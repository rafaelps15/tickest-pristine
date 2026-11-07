using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.Infrastructure.Tickets;

internal sealed class TicketActivityProjector(ApplicationDbContext context, IAgentDirectory agentDirectory)
    : ITicketActivityProjector
{
    public async Task ProjectAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // A trava vem antes das leituras: uma ação gravada depois delas só é projetada depois deste commit, e a projeção
        // dela lê o que esta não viu. FOR NO KEY UPDATE é a trava do próprio UPDATE e não bloqueia a gravação de mensagens
        // e anexos do chamado, cujas chaves estrangeiras só pedem FOR KEY SHARE.
        await context.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM public.tickets WHERE id = {ticketId} FOR NO KEY UPDATE",
            cancellationToken);

        Ticket? ticket = await context.Tickets
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket is null)
        {
            return;
        }

        // Toda ação no chamado grava uma entrada no histórico: a última atividade é a mais recente delas.
        DateTime? lastActivityAtUtc = await context.TicketHistories
            .Where(h => h.TicketId == ticketId)
            .MaxAsync(h => (DateTime?)h.OccurredAtUtc, cancellationToken);

        // O filtro global exclui as mensagens excluídas: a primeira na ordem é a última mensagem válida.
        Guid? lastAuthorUserId = await context.TicketMessages
            .Where(m => m.TicketId == ticketId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Select(m => (Guid?)m.AuthorUserId)
            .FirstOrDefaultAsync(cancellationToken);

        bool lastMessageIsFromAgent = lastAuthorUserId is { } authorUserId &&
            ticket.IsAgentReply(authorUserId, await agentDirectory.IsAgentAsync(authorUserId, cancellationToken));

        string? waitingOn = ticket.WaitingOnAfter(lastMessageIsFromAgent)?.ToString();

        // GREATEST ignora o nulo, e a condição evita mudar a versão da linha quando as duas colunas já estão certas.
        await context.Database.ExecuteSqlAsync(
            $"""
            UPDATE public.tickets
            SET last_activity_at_utc = GREATEST(last_activity_at_utc, {lastActivityAtUtc}),
                waiting_on = {waitingOn}
            WHERE id = {ticketId}
              AND (last_activity_at_utc < {lastActivityAtUtc} OR waiting_on IS DISTINCT FROM {waitingOn})
            """,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
