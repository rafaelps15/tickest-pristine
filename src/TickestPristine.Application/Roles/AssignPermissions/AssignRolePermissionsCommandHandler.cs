using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
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
        Role? role = await RoleQueries.WithPermissions(context.Roles)
            .SingleOrDefaultAsync(candidate => candidate.Name == command.RoleName, cancellationToken);

        if (role is null)
        {
            return Result.Failure(RoleErrors.NotFound(command.RoleName));
        }

        HashSet<string> callerPermissionCodes = await permissionProvider.GetCurrentForUserIdAsync(
            userContext.UserId,
            cancellationToken);
        Result<RolePermissionsChange> change = role.ReplacePermissions(command.PermissionCodes, callerPermissionCodes);

        if (change.IsFailure)
        {
            return Result.Failure(change.Error);
        }

        await context.SaveChangesAsync(cancellationToken);

        List<Guid> affectedUserIds = await context.UserRoles
            .Where(userRole => userRole.RoleName == role.Name)
            .Select(userRole => userRole.UserId)
            .ToListAsync(CancellationToken.None);

        foreach (Guid userId in affectedUserIds)
        {
            await permissionProvider.InvalidateAsync(userId, CancellationToken.None);
        }

        return Result.Success();
    }
}
