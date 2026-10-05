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
        if (role.Id == Role.Administrator.Id)
        {
            return Result.Failure(RoleErrors.AdministratorRoleIsManaged);
        }

        List<RolePermission> existingPermissions = await context.RolePermissions
            .Where(p => p.RoleId == role.Id)
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

        // A chave é (RoleId, PermissionCode): remove só as que saíram e inclui só as que entraram.
        context.RolePermissions.RemoveRange(existingPermissions.Where(p => !requestedCodes.Contains(p.PermissionCode)));

        foreach (string permissionCode in requestedCodes.Except(currentCodes))
        {
            context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = permissionCode });
        }

        List<Guid> affectedUserIds = await context.UserRoles
            .Where(userRole => userRole.RoleId == role.Id)
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
