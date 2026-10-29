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

        // O pedido traz nomes; os vínculos guardam o id da role (spec 036).
        HashSet<string> requestedRoleNames = [.. command.RoleNames];

        Dictionary<string, Guid> requestedRoleIdsByName = await context.Roles
            .Where(r => requestedRoleNames.Contains(r.Name))
            .ToDictionaryAsync(r => r.Name, r => r.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        string? missingRoleName = requestedRoleNames.FirstOrDefault(name => !requestedRoleIdsByName.ContainsKey(name));

        if (missingRoleName is not null)
        {
            return Result.Failure(RoleErrors.NotFound(missingRoleName));
        }

        HashSet<Guid> requestedRoleIds = [.. requestedRoleIdsByName.Values];

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

        if (currentRoleIds.Contains(RoleIds.Administrator) && !requestedRoleIds.Contains(RoleIds.Administrator))
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

        // Depois do commit, a invalidação não pode ser cancelada junto com a requisição: o banco já mudou.
        await permissionProvider.InvalidateAsync(user.Id, CancellationToken.None);

        return Result.Success();
    }
}
