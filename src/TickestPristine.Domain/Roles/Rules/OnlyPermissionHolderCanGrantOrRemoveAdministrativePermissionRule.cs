using TickestPristine.Domain.Permissions;
using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles.Rules;

public sealed class OnlyPermissionHolderCanGrantOrRemoveAdministrativePermissionRule : IBusinessRule
{
    private readonly IReadOnlyCollection<string> _changedPermissionCodes;
    private readonly IReadOnlySet<string> _callerPermissionCodes;

    public OnlyPermissionHolderCanGrantOrRemoveAdministrativePermissionRule(
        IReadOnlyCollection<string> changedPermissionCodes,
        IReadOnlySet<string> callerPermissionCodes)
    {
        _changedPermissionCodes = changedPermissionCodes;
        _callerPermissionCodes = callerPermissionCodes;
    }

    public bool IsBroken() => _changedPermissionCodes.Any(code =>
        PermissionCatalog.Find(code)?.IsAdministrative == true && !_callerPermissionCodes.Contains(code));

    public Error Error
    {
        get
        {
            IEnumerable<string> missingPermissions = _changedPermissionCodes
                .Where(code => PermissionCatalog.Find(code)?.IsAdministrative == true && !_callerPermissionCodes.Contains(code))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(PermissionCatalog.GetDisplayName);

            return RoleErrors.PrivilegeEscalation(missingPermissions);
        }
    }
}
