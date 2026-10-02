namespace TickestPristine.Application.Authorization;

internal static class PrivilegeEscalationGuard
{
    /// <summary>
    /// Retorna as permissões administrativas alteradas que o usuário atual não possui; lista vazia indica que a alteração é permitida.
    /// </summary>
    public static List<string> FindPermissionsCallerCannotChange(
        IEnumerable<string> changedPermissions,
        IReadOnlySet<string> callerPermissions) =>
        changedPermissions
            .Where(permission => PermissionCodes.Administrative.Contains(permission))
            .Where(permission => !callerPermissions.Contains(permission))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();
}
