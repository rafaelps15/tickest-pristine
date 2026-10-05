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
        TicketHistory history = TicketHistoryEntries.StatusChanged(
            domainEvent.TicketId,
            domainEvent.ChangedByUserId,
            domainEvent.OldStatus,
            domainEvent.NewStatus,
            dateTimeProvider.UtcNow);

        history.Raise(new TicketHistoryRecordedDomainEvent(history.Id, history.TicketId));

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
