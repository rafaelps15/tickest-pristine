using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Domain.Permissions;

namespace TickestPristine.Application.Users;

/// <summary>
/// Responde se um usuário atende chamados: conta ativa com a permissão <c>tickets:manage</c> em alguma das suas roles.
/// Lê as permissões sem o cache, para a resposta valer no mesmo instante em que a role muda.
/// </summary>
internal sealed class AgentDirectory(IPermissionProvider permissionProvider) : IAgentDirectory
{
    public async Task<bool> IsAgentAsync(Guid userId, CancellationToken cancellationToken)
    {
        HashSet<string> permissions = await permissionProvider.GetCurrentForUserIdAsync(userId, cancellationToken);

        return permissions.Contains(PermissionCodes.Tickets.Manage);
    }
}

