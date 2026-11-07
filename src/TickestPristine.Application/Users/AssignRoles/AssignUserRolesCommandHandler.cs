using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.AssignRoles;

/// <summary>
/// Define as funções do usuário (o endpoint exige <c>users:assign-roles</c>) no lugar das atuais. Quem chama não concede
/// nem remove permissões administrativas que não possui, e o último administrador não perde a função. Informar as funções
/// atuais não muda nada.
/// </summary>
internal sealed class AssignUserRolesCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    PrivilegeEscalationCheck privilegeEscalationCheck,
    LastAdministratorCheck lastAdministratorCheck)
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

        List<string> foundRoleNames = await context.Roles
            .Where(r => requestedRoleNames.Contains(r.Name))
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        string? unknownRoleName = requestedRoleNames.Except(foundRoleNames).FirstOrDefault();

        if (unknownRoleName is not null)
        {
            return Result.Failure(RoleErrors.NotFound(unknownRoleName));
        }

        List<UserRole> currentUserRoles = await context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .ToListAsync(cancellationToken);

        HashSet<string> currentRoleNames = [.. currentUserRoles.Select(ur => ur.RoleName)];

        // As funções que entram e as que saem.
        HashSet<string> changedRoleNames = [.. currentRoleNames];
        changedRoleNames.SymmetricExceptWith(requestedRoleNames);

        if (changedRoleNames.Count == 0)
        {
            return Result.Success();
        }

        List<string> changedPermissions = await context.RolePermissions
            .Where(p => changedRoleNames.Contains(p.RoleName))
            .Select(p => p.PermissionCode)
            .ToListAsync(cancellationToken);

        List<string> missingPermissions = await privilegeEscalationCheck.GetMissingPermissionsToChangeAsync(
            userContext.UserId,
            changedPermissions,
            cancellationToken);

        if (missingPermissions.Count > 0)
        {
            return Result.Failure(RoleErrors.PrivilegeEscalation(missingPermissions));
        }

        bool removesAdministratorRole =
            currentRoleNames.Contains(RoleNames.Administrator) && !requestedRoleNames.Contains(RoleNames.Administrator);

        if (removesAdministratorRole &&
            await lastAdministratorCheck.IsLastAdministratorWithLockAsync(user.Id, cancellationToken))
        {
            return Result.Failure(RoleErrors.LastAdministrator);
        }

        context.UserRoles.RemoveRange(currentUserRoles.Where(ur => !requestedRoleNames.Contains(ur.RoleName)));
        context.UserRoles.AddRange(requestedRoleNames
            .Except(currentRoleNames)
            .Select(roleName => new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleName = roleName }));

        user.Raise(new UserRolesAssignedDomainEvent(user.Id));

        await context.SaveChangesAsync(cancellationToken);

        // Depois do commit, a invalidação não pode ser cancelada junto com a requisição: o banco já mudou.
        await permissionProvider.InvalidateAsync(user.Id, CancellationToken.None);

        return Result.Success();
    }
}
