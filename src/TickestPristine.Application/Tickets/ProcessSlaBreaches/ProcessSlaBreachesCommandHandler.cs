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

        List<TicketSlaTimer> timers = await context.TicketSlaTimers
            .Where(t => t.NextActionOnUtc != null && t.NextActionOnUtc <= nowUtc)
            .OrderBy(t => t.NextActionOnUtc)
            .ThenBy(t => t.Id)
            .Take(command.BatchSize)
            .ToListAsync(cancellationToken);

        if (timers.Count == 0)
        {
            return new ProcessSlaBreachesResponse(0, 0);
        }

        int breached = timers.Count(timer => timer.ProcessDue());

        await context.SaveChangesAsync(cancellationToken);

        return new ProcessSlaBreachesResponse(timers.Count, breached);
    }
}
