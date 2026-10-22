using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Permissions;
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
        Role? role = await context.Roles.SingleOrDefaultAsync(r => r.Name == command.RoleName, cancellationToken);

        if (role is null)
        {
            return Result.Failure(RoleErrors.NotFound(command.RoleName));
        }

        // A role de administrador sempre recebe todas as permissões, por isso não pode ser editada.
        if (role.Name == RoleNames.Administrator)
        {
            return Result.Failure(RoleErrors.AdministratorRoleIsManaged);
        }

        List<RolePermission> existingPermissions = await context.RolePermissions
            .Where(p => p.RoleName == role.Name)
            .ToListAsync(cancellationToken);

        HashSet<string> currentCodes = [.. existingPermissions.Select(p => p.PermissionCode)];
        HashSet<string> requestedCodes = [.. command.PermissionCodes];

        // Considera tanto as permissões adicionadas quanto as removidas.
        HashSet<string> changedCodes = [.. currentCodes];
        changedCodes.SymmetricExceptWith(requestedCodes);

        Result escalationCheck = await PrivilegeEscalationGuard.EnsureCallerCanChangeAsync(
            permissionProvider,
            userContext.UserId,
            changedCodes,
            cancellationToken);

        if (escalationCheck.IsFailure)
        {
            return escalationCheck;
        }

        // O par (RoleName, PermissionCode) é único: remove só as que saíram e inclui só as que entraram.
        context.RolePermissions.RemoveRange(existingPermissions.Where(p => !requestedCodes.Contains(p.PermissionCode)));

        foreach (string permissionCode in requestedCodes.Except(currentCodes))
        {
            context.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleName = role.Name,
                PermissionCode = permissionCode
            });
        }

        role.Raise(new RolePermissionsAssignedDomainEvent(role.Id));

        List<Guid> affectedUserIds = await context.UserRoles
            .Where(userRole => userRole.RoleName == role.Name)
            .Select(userRole => userRole.UserId)
            .ToListAsync(cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        // Depois do commit, a invalidação não pode ser cancelada junto com a requisição: o banco já mudou.
        foreach (Guid userId in affectedUserIds)
        {
            await permissionProvider.InvalidateAsync(userId, CancellationToken.None);
        }

        return Result.Success();
    }
}
