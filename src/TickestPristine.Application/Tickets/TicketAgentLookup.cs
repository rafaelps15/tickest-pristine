using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets;

/// <summary>
/// Perguntas do chamado que dependem de quem atende (<see cref="IAgentDirectory"/>), lidas do banco, sem o cache de
/// permissões, para a situação de espera do chamado nunca discordar da lista de atendentes.
/// </summary>
internal sealed class TicketAgentLookup(IApplicationDbContext context, IAgentDirectory agentDirectory)
{
    /// <summary>
    /// Indica se a última mensagem válida do chamado (a mais recente não excluída) é de quem atende e não do
    /// solicitante; sem mensagens, devolve <c>false</c>.
    /// </summary>
    public async Task<bool> IsLastValidMessageFromAgentAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        Guid? authorUserId = await context.TicketMessages
            .Where(m => m.TicketId == ticket.Id)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Select(m => (Guid?)m.AuthorUserId)
            .FirstOrDefaultAsync(cancellationToken);

        return authorUserId is { } authorId &&
            ticket.IsAgentReply(authorId, await agentDirectory.IsAgentAsync(authorId, cancellationToken));
    }
}
