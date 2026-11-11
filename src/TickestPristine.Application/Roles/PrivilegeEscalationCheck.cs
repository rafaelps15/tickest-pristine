using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Domain.Permissions;

namespace TickestPristine.Application.Roles;

/// <summary>
/// Conferência de escalada de privilégio, comum aos handlers de funções e de usuários: diz quais permissões
/// administrativas quem chama não possui para a operação, e o handler devolve o erro. As permissões de quem chama são
/// lidas sem o cache, porque a decisão grava.
/// </summary>
internal sealed class PrivilegeEscalationCheck(IPermissionProvider permissionProvider)
{
    /// <summary>
    /// Nomes das permissões administrativas que mudam (as que entram e as que saem) e que quem chama não possui, em ordem;
    /// vazio quando ele pode fazer a mudança.
    /// </summary>
    public Task<List<string>> GetMissingPermissionsToChangeAsync(
        Guid callerId,
        IEnumerable<string> changedPermissions,
        CancellationToken cancellationToken) =>
        FindMissingAdministrativePermissionsAsync(callerId, changedPermissions, cancellationToken);

    /// <summary>
    /// Nomes das permissões administrativas do usuário-alvo que quem chama não possui, em ordem; vazio quando ele pode
    /// gerenciar o alvo. As permissões do alvo são as que as funções dele concedem, lidas sem o cache, que fica vazio
    /// enquanto o usuário está inativo.
    /// </summary>
    public async Task<List<string>> GetMissingPermissionsToManageAsync(
        Guid callerId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        HashSet<string> targetPermissions = await permissionProvider.GetGrantedByRolesAsync(
            targetUserId,
            cancellationToken);

        return await FindMissingAdministrativePermissionsAsync(callerId, targetPermissions, cancellationToken);
    }

    /// <summary>
    /// Nomes das permissões administrativas da lista que quem chama não possui, na ordem dos códigos; só consulta as
    /// permissões dele, sem o cache, quando a lista tem alguma administrativa.
    /// </summary>
    private async Task<List<string>> FindMissingAdministrativePermissionsAsync(
        Guid callerId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken)
    {
        var administrative = permissions
            .Where(code => PermissionCatalog.Find(code)?.IsAdministrative == true)
            .Distinct()
            .ToList();

        if (administrative.Count == 0)
        {
            return [];
        }

        HashSet<string> callerPermissions = await permissionProvider.GetCurrentForUserIdAsync(callerId, cancellationToken);

        return administrative
            .Where(permission => !callerPermissions.Contains(permission))
            .Order(StringComparer.Ordinal)
            .Select(PermissionCatalog.GetDisplayName)
            .ToList();
    }
}

