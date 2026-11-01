using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.Infrastructure.Tickets;

internal sealed class TicketActivityWriter(ApplicationDbContext context) : ITicketActivityWriter
{
    // GREATEST: a data nunca volta, mesmo com duas ações gravadas fora de ordem.
    public async Task WriteLastActivityAsync(Ticket ticket, CancellationToken cancellationToken) =>
        await context.Database.ExecuteSqlAsync(
            $"""
            UPDATE public.tickets
            SET last_activity_at_utc = GREATEST(last_activity_at_utc, {ticket.LastActivityAtUtc})
            WHERE id = {ticket.Id}
            """,
            cancellationToken);

    /// <summary>
    /// Grava a última atividade como <see cref="WriteLastActivityAsync"/> e a situação de espera do chamado. Se o status
    /// mudou no banco depois que o chamado foi lido, vale o status gravado: o chamado inativo fica sem situação, e o
    /// chamado lido como inativo mantém a situação que já tinha.
    /// </summary>
    public async Task WriteLastActivityAndWaitingOnAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        string? waitingOn = ticket.WaitingOn?.ToString();

        await context.Database.ExecuteSqlAsync(
            $"""
            UPDATE public.tickets
            SET last_activity_at_utc = GREATEST(last_activity_at_utc, {ticket.LastActivityAtUtc}),
                waiting_on = CASE
                    WHEN status IN ({nameof(TicketStatus.Open)}, {nameof(TicketStatus.InProgress)}) THEN COALESCE({waitingOn}, waiting_on)
                    ELSE NULL
                END
            WHERE id = {ticket.Id}
            """,
            cancellationToken);
    }
}
