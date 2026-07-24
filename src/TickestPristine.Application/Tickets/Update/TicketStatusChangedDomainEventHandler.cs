using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Update;

internal sealed class TicketStatusChangedDomainEventHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketStatusChangedDomainEvent>
{
    public async Task Handle(TicketStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var history = TicketHistory.Create(
            domainEvent.TicketId,
            domainEvent.ChangedByUserId,
            TicketHistoryAction.StatusChanged,
            $"Status alterado de {domainEvent.OldStatus} para {domainEvent.NewStatus}",
            oldValue: domainEvent.OldStatus.ToString(),
            newValue: domainEvent.NewStatus.ToString(),
            dateTimeProvider.UtcNow);

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
