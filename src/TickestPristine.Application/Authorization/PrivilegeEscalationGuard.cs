using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Authorization;

internal static class PrivilegeEscalationGuard
{
    /// <summary>
    /// Impede quem chama de conceder ou remover permissões administrativas que não possui. Recebe as permissões que
    /// mudam (as que entram e as que saem).
    /// </summary>
    public static async Task<Result> EnsureCallerCanChangeAsync(
        IPermissionProvider permissionProvider,
        Guid callerId,
        IEnumerable<string> changedPermissions,
        CancellationToken cancellationToken)
    {
        List<string> missing = await FindMissingAdministrativePermissionsAsync(
            permissionProvider,
            callerId,
            changedPermissions,
            cancellationToken);

        return missing.Count == 0
            ? Result.Success()
            : Result.Failure(RoleErrors.PrivilegeEscalation(missing.Select(Permission.GetDisplayName)));
    }

    /// <summary>
    /// Impede quem chama de desativar ou reativar um usuário que tem permissões administrativas que ele não possui.
    /// As permissões do alvo saem das roles dele, e não do cache, que fica vazio enquanto o usuário está inativo.
    /// </summary>
    public static async Task<Result> EnsureCallerCanManageUserAsync(
        IApplicationDbContext context,
        IPermissionProvider permissionProvider,
        Guid callerId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        List<string> targetPermissions = await (
            from userRole in context.UserRoles
            where userRole.UserId == targetUserId
            join rolePermission in context.RolePermissions on userRole.RoleName equals rolePermission.RoleName
            select rolePermission.PermissionCode)
            .Distinct()
            .ToListAsync(cancellationToken);

        List<string> missing = await FindMissingAdministrativePermissionsAsync(
            permissionProvider,
            callerId,
            targetPermissions,
            cancellationToken);

        return missing.Count == 0
            ? Result.Success()
            : Result.Failure(UserErrors.CannotManagePrivilegedUser(missing.Select(Permission.GetDisplayName)));
    }

    /// <summary>
    /// Permissões administrativas da lista que quem chama não tem, em ordem; só consulta as permissões dele quando a
    /// lista tem alguma administrativa.
    /// </summary>
    private static async Task<List<string>> FindMissingAdministrativePermissionsAsync(
        IPermissionProvider permissionProvider,
        Guid callerId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken)
    {
        var administrative = permissions
            .Where(code => Permission.Find(code)?.IsAdministrative == true)
            .Distinct()
            .ToList();

        if (administrative.Count == 0)
        {
            return [];
        }

        HashSet<string> callerPermissions = await permissionProvider.GetForUserIdAsync(callerId, cancellationToken);

        return administrative
            .Where(permission => !callerPermissions.Contains(permission))
            .Order(StringComparer.Ordinal)
            .ToList();
    }
}
