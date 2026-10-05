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

        HashSet<Guid> requestedRoleIds = [.. command.RoleIds];

        List<Guid> existingRoleIds = await context.Roles
            .Where(r => requestedRoleIds.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        Guid? missingRoleId = requestedRoleIds.Except(existingRoleIds).Cast<Guid?>().FirstOrDefault();

        if (missingRoleId is { } roleId)
        {
            return Result.Failure(RoleErrors.NotFound(roleId));
        }

        List<UserRole> existingUserRoles = await context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .ToListAsync(cancellationToken);

        HashSet<Guid> currentRoleIds = [.. existingUserRoles.Select(ur => ur.RoleId)];

        // Considera tanto as roles adicionadas quanto as removidas.
        HashSet<Guid> changedRoleIds = [.. currentRoleIds];
        changedRoleIds.SymmetricExceptWith(requestedRoleIds);

        List<string> changedPermissions = await context.RolePermissions
            .Where(p => changedRoleIds.Contains(p.RoleId))
            .Select(p => p.PermissionCode)
            .ToListAsync(cancellationToken);

        Result escalationCheck = await PrivilegeEscalationGuard.EnsureCallerCanChangeAsync(
            permissionProvider,
            userContext.UserId,
            changedPermissions,
            cancellationToken);

        if (escalationCheck.IsFailure)
        {
            return escalationCheck;
        }

        if (currentRoleIds.Contains(Role.Administrator.Id) && !requestedRoleIds.Contains(Role.Administrator.Id))
        {
            Result administratorCheck = await AdministratorGuard.EnsureNotLastAdministratorAsync(
                context,
                user.Id,
                cancellationToken);

            if (administratorCheck.IsFailure)
            {
                return administratorCheck;
            }
        }

        context.UserRoles.RemoveRange(existingUserRoles.Where(ur => !requestedRoleIds.Contains(ur.RoleId)));

        foreach (Guid addedRoleId in requestedRoleIds.Except(currentRoleIds))
        {
            context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleId = addedRoleId });
        }

        user.Raise(new UserRolesAssignedDomainEvent(user.Id));

        await context.SaveChangesAsync(cancellationToken);

        await permissionProvider.InvalidateAsync(user.Id, cancellationToken);

        return Result.Success();
    }
}
