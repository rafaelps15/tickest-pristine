using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.ProcessSlaBreaches;

/// <summary>
/// Grava a violação dos cronômetros vencidos de um lote e dispara um evento por violação. Tudo num único
/// <c>SaveChangesAsync</c>, pela auditoria: um conflito com a conclusão ou a parada (tokens de concorrência do
/// cronômetro) desfaz o lote inteiro, que volta na execução seguinte.
/// </summary>
internal sealed class ProcessSlaBreachesCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ProcessSlaBreachesCommand, ProcessSlaBreachesResponse>
{
    public async Task<Result<ProcessSlaBreachesResponse>> Handle(
        ProcessSlaBreachesCommand command,
        CancellationToken cancellationToken)
    {
        DateTime nowUtc = dateTimeProvider.UtcNow;

        var dueTimers = await context.Tickets
            .SelectMany(ticket => ticket.SlaTimers
                .Where(timer => timer.NextActionOnUtc != null && timer.NextActionOnUtc <= nowUtc)
                .Select(timer => new { timer.TicketId, timer.Id, timer.NextActionOnUtc }))
            .OrderBy(timer => timer.NextActionOnUtc)
            .ThenBy(timer => timer.Id)
            .Take(command.BatchSize)
            .ToListAsync(cancellationToken);

        if (dueTimers.Count == 0)
        {
            return new ProcessSlaBreachesResponse(0, 0);
        }

        Guid[] ticketIds = dueTimers.Select(timer => timer.TicketId).Distinct().ToArray();
        List<Ticket> tickets = await context.Tickets
            .Include(ticket => ticket.SlaTimers)
            .Where(ticket => ticketIds.Contains(ticket.Id))
            .ToListAsync(cancellationToken);

        var ticketsById = tickets.ToDictionary(ticket => ticket.Id);
        int breached = dueTimers.Count(timer => ticketsById[timer.TicketId].SettleSlaDeadline(timer.Id));

        await context.SaveChangesAsync(cancellationToken);

        return new ProcessSlaBreachesResponse(dueTimers.Count, breached);
    }
}
