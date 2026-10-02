using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.AssignRoles;

internal sealed class AssignUserRolesCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : ICommandHandler<AssignUserRolesCommand>
{
    public async Task<Result> Handle(AssignUserRolesCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users.SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        var distinctRoleIds = command.RoleIds.Distinct().ToList();

        List<Guid> existingRoleIds = await context.Roles
            .Where(r => distinctRoleIds.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        Guid? missingRoleId = distinctRoleIds.Except(existingRoleIds).Cast<Guid?>().FirstOrDefault();

        if (missingRoleId is { } roleId)
        {
            return Result.Failure(RoleErrors.NotFound(roleId));
        }

        List<UserRole> existingUserRoles = await context.UserRoles
            .Where(ur => ur.UserId == command.UserId)
            .ToListAsync(cancellationToken);

        // Considera tanto as roles adicionadas quanto as removidas.
        var currentRoleIds = existingUserRoles.Select(ur => ur.RoleId).ToList();
        var changedRoleIds = distinctRoleIds.Except(currentRoleIds)
            .Union(currentRoleIds.Except(distinctRoleIds))
            .ToList();

        List<string> changedPermissions = await context.RolePermissions
            .Where(p => changedRoleIds.Contains(p.RoleId))
            .Select(p => p.PermissionCode)
            .ToListAsync(cancellationToken);

        HashSet<string> callerPermissions = await permissionProvider.GetForUserIdAsync(userContext.UserId, cancellationToken);

        List<string> forbidden = PrivilegeEscalationGuard.FindPermissionsCallerCannotChange(changedPermissions, callerPermissions);

        if (forbidden.Count > 0)
        {
            return Result.Failure(RoleErrors.PrivilegeEscalation(forbidden.Select(PermissionCodes.GetDisplayName)));
        }

        Role? administratorRole = await context.Roles.SingleOrDefaultAsync(r => r.IsAdministrator, cancellationToken);

        if (administratorRole is not null &&
            currentRoleIds.Contains(administratorRole.Id) &&
            !distinctRoleIds.Contains(administratorRole.Id))
        {
            Result administratorCheck = await AdministratorGuard.EnsureAnotherActiveAdministratorAsync(
                context,
                administratorRole,
                command.UserId,
                cancellationToken);

            if (administratorCheck.IsFailure)
            {
                return administratorCheck;
            }
        }

        context.UserRoles.RemoveRange(existingUserRoles);

        foreach (Guid distinctRoleId in distinctRoleIds)
        {
            context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = command.UserId, RoleId = distinctRoleId });
        }

        user.Raise(new UserRolesAssignedDomainEvent(user.Id));

        await context.SaveChangesAsync(cancellationToken);

        await permissionProvider.InvalidateAsync(command.UserId, cancellationToken);

        return Result.Success();
    }
}
