using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.SettleDueSlaDeadlines;

/// <summary>
/// Liquida pelo agregado os prazos que continuam vencidos e salva o lote uma vez.
/// </summary>
internal sealed class SettleDueSlaDeadlinesCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<SettleDueSlaDeadlinesCommand, SettleDueSlaDeadlinesResponse>
{
    public async Task<Result<SettleDueSlaDeadlinesResponse>> Handle(
        SettleDueSlaDeadlinesCommand command,
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
            return Result.Success(new SettleDueSlaDeadlinesResponse(0, 0));
        }

        Guid[] ticketIds = dueTimers.Select(timer => timer.TicketId).Distinct().ToArray();
        List<Ticket> tickets = await context.Tickets
            .Include(ticket => ticket.SlaTimers)
            .Where(ticket => ticketIds.Contains(ticket.Id))
            .ToListAsync(cancellationToken);

        var ticketsById = tickets.ToDictionary(ticket => ticket.Id);
        int processed = 0;
        int breached = 0;

        foreach (var timer in dueTimers)
        {
            if (!ticketsById.TryGetValue(timer.TicketId, out Ticket? ticket))
            {
                continue;
            }

            TicketSlaTimer? slaTimer = ticket.SlaTimers.SingleOrDefault(candidate => candidate.Id == timer.Id);
            if (slaTimer is null ||
                slaTimer.NextActionOnUtc != timer.NextActionOnUtc ||
                !slaTimer.IsDueForMonitoring(nowUtc))
            {
                continue;
            }

            processed++;
            if (ticket.SettleSlaDeadline(slaTimer.Id))
            {
                breached++;
            }
        }

        if (processed == 0)
        {
            return Result.Success(new SettleDueSlaDeadlinesResponse(0, 0));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new SettleDueSlaDeadlinesResponse(processed, breached));
    }
}
