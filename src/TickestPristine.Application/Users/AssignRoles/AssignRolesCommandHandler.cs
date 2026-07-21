using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.AssignRoles;

internal sealed class AssignRolesCommandHandler(
    IApplicationDbContext context,
    IPermissionProvider permissionProvider)
    : ICommandHandler<AssignRolesCommand>
{
    public async Task<Result> Handle(AssignRolesCommand command, CancellationToken cancellationToken)
    {
        bool userExists = await context.Users.AnyAsync(u => u.Id == command.UserId, cancellationToken);

        if (!userExists)
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

        context.UserRoles.RemoveRange(existingUserRoles);

        foreach (Guid distinctRoleId in distinctRoleIds)
        {
            context.UserRoles.Add(UserRole.Create(command.UserId, distinctRoleId));
        }

        await context.SaveChangesAsync(cancellationToken);

        await permissionProvider.InvalidateAsync(command.UserId, cancellationToken);

        return Result.Success();
    }
}
