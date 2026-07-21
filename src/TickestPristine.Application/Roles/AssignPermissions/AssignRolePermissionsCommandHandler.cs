using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Roles.AssignPermissions;

internal sealed class AssignRolePermissionsCommandHandler(
    IApplicationDbContext context,
    IPermissionProvider permissionProvider)
    : ICommandHandler<AssignRolePermissionsCommand>
{
    public async Task<Result> Handle(AssignRolePermissionsCommand command, CancellationToken cancellationToken)
    {
        bool roleExists = await context.Roles.AnyAsync(r => r.Id == command.RoleId, cancellationToken);

        if (!roleExists)
        {
            return Result.Failure(RoleErrors.NotFound(command.RoleId));
        }

        List<RolePermission> existingPermissions = await context.RolePermissions
            .Where(p => p.RoleId == command.RoleId)
            .ToListAsync(cancellationToken);

        context.RolePermissions.RemoveRange(existingPermissions);

        foreach (string permissionCode in command.PermissionCodes.Distinct())
        {
            context.RolePermissions.Add(RolePermission.Create(command.RoleId, permissionCode));
        }

        List<Guid> affectedUserIds = await context.UserRoles
            .Where(userRole => userRole.RoleId == command.RoleId)
            .Select(userRole => userRole.UserId)
            .ToListAsync(cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        foreach (Guid userId in affectedUserIds)
        {
            await permissionProvider.InvalidateAsync(userId, cancellationToken);
        }

        return Result.Success();
    }
}
