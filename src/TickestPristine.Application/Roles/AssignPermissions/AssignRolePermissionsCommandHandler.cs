using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Roles.AssignPermissions;

internal sealed class AssignRolePermissionsCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : ICommandHandler<AssignRolePermissionsCommand>
{
    public async Task<Result> Handle(AssignRolePermissionsCommand command, CancellationToken cancellationToken)
    {
        Role? role = await context.Roles.SingleOrDefaultAsync(r => r.Id == command.RoleId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(RoleErrors.NotFound(command.RoleId));
        }

        // A role de administrador sempre recebe todas as permissões, por isso não pode ser editada.
        if (role.IsAdministrator)
        {
            return Result.Failure(RoleErrors.AdministratorRoleIsManaged);
        }

        List<RolePermission> existingPermissions = await context.RolePermissions
            .Where(p => p.RoleId == command.RoleId)
            .ToListAsync(cancellationToken);

        // Considera tanto as permissões adicionadas quanto as removidas.
        var currentPermissions = existingPermissions.Select(p => p.PermissionCode).ToList();
        var changedPermissions = command.PermissionCodes.Except(currentPermissions)
            .Union(currentPermissions.Except(command.PermissionCodes))
            .ToList();

        HashSet<string> callerPermissions = await permissionProvider.GetForUserIdAsync(userContext.UserId, cancellationToken);

        List<string> forbidden = PrivilegeEscalationGuard.FindPermissionsCallerCannotChange(changedPermissions, callerPermissions);

        if (forbidden.Count > 0)
        {
            return Result.Failure(RoleErrors.PrivilegeEscalation(forbidden.Select(PermissionCodes.GetDisplayName)));
        }

        // A chave é (RoleId, PermissionCode): remove só as que saíram e inclui só as que entraram.
        context.RolePermissions.RemoveRange(
            existingPermissions.Where(p => !command.PermissionCodes.Contains(p.PermissionCode)));

        foreach (string permissionCode in command.PermissionCodes.Except(currentPermissions))
        {
            context.RolePermissions.Add(new RolePermission { RoleId = command.RoleId, PermissionCode = permissionCode });
        }

        List<Guid> affectedUserIds = await context.UserRoles
            .Where(userRole => userRole.RoleId == command.RoleId)
            .Select(userRole => userRole.UserId)
            .ToListAsync(cancellationToken);

        role.Raise(new RolePermissionsAssignedDomainEvent(role.Id));

        await context.SaveChangesAsync(cancellationToken);

        foreach (Guid userId in affectedUserIds)
        {
            await permissionProvider.InvalidateAsync(userId, cancellationToken);
        }

        return Result.Success();
    }
}
