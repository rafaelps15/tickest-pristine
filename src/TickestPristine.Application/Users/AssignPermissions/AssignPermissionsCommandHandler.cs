using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.AssignPermissions;

internal sealed class AssignPermissionsCommandHandler(
    IApplicationDbContext context,
    IPermissionProvider permissionProvider)
    : ICommandHandler<AssignPermissionsCommand>
{
    public async Task<Result> Handle(AssignPermissionsCommand command, CancellationToken cancellationToken)
    {
        bool userExists = await context.Users.AnyAsync(u => u.Id == command.UserId, cancellationToken);

        if (!userExists)
        {
            return Result.Failure(UserErrors.NotFound(command.UserId));
        }

        List<UserPermission> existingPermissions = await context.UserPermissions
            .Where(p => p.UserId == command.UserId)
            .ToListAsync(cancellationToken);

        context.UserPermissions.RemoveRange(existingPermissions);

        foreach (string permissionCode in command.PermissionCodes.Distinct())
        {
            context.UserPermissions.Add(UserPermission.Create(command.UserId, permissionCode));
        }

        await context.SaveChangesAsync(cancellationToken);

        await permissionProvider.InvalidateAsync(command.UserId, cancellationToken);

        return Result.Success();
    }
}
