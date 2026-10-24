using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Roles;
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

        HashSet<string> requestedRoleNames = [.. command.RoleNames];

        List<string> existingRoleNames = await context.Roles
            .Where(r => requestedRoleNames.Contains(r.Name))
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        string? missingRoleName = requestedRoleNames.Except(existingRoleNames).FirstOrDefault();

        if (missingRoleName is not null)
        {
            return Result.Failure(RoleErrors.NotFound(missingRoleName));
        }

        List<UserRole> existingUserRoles = await context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .ToListAsync(cancellationToken);

        HashSet<string> currentRoleNames = [.. existingUserRoles.Select(ur => ur.RoleName)];

        // Considera tanto as roles adicionadas quanto as removidas.
        HashSet<string> changedRoleNames = [.. currentRoleNames];
        changedRoleNames.SymmetricExceptWith(requestedRoleNames);

        List<string> changedPermissions = await context.RolePermissions
            .Where(p => changedRoleNames.Contains(p.RoleName))
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

        if (currentRoleNames.Contains(RoleNames.Administrator) && !requestedRoleNames.Contains(RoleNames.Administrator))
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

        context.UserRoles.RemoveRange(existingUserRoles.Where(ur => !requestedRoleNames.Contains(ur.RoleName)));

        foreach (string addedRoleName in requestedRoleNames.Except(currentRoleNames))
        {
            context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleName = addedRoleName });
        }

        user.Raise(new UserRolesAssignedDomainEvent(user.Id));

        await context.SaveChangesAsync(cancellationToken);

        // Depois do commit, a invalidação não pode ser cancelada junto com a requisição: o banco já mudou.
        await permissionProvider.InvalidateAsync(user.Id, CancellationToken.None);

        return Result.Success();
    }
}
