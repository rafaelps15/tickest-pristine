namespace TickestPristine.Application.Abstractions.Authorization;

/// <summary>
/// Quem atende chamados: usuário ativo com a permissão efetiva <c>tickets:manage</c>, vinda de qualquer uma das suas
/// roles. Lê do banco, sem o cache de permissões, para a atribuição e a situação de espera do chamado nunca discordarem
/// da lista de atendentes.
/// </summary>
public interface IAgentDirectory
{
    /// <summary>
    /// Se o usuário atende chamados agora; falso para usuário inexistente, desativado ou sem a permissão.
    /// </summary>
    Task<bool> IsAgentAsync(Guid userId, CancellationToken cancellationToken);
}
