using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Domain.Roles;
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
        var administrativeChanges = changedPermissions
            .Where(code => Permission.Find(code)?.IsAdministrative == true)
            .Distinct()
            .ToList();

        if (administrativeChanges.Count == 0)
        {
            return Result.Success();
        }

        HashSet<string> callerPermissions = await permissionProvider.GetForUserIdAsync(callerId, cancellationToken);

        var forbidden = administrativeChanges
            .Where(permission => !callerPermissions.Contains(permission))
            .Order(StringComparer.Ordinal)
            .ToList();

        return forbidden.Count == 0
            ? Result.Success()
            : Result.Failure(RoleErrors.PrivilegeEscalation(forbidden.Select(Permission.GetDisplayName)));
    }
}
