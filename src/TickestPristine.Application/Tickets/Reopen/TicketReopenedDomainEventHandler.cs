using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Reopen;

internal sealed class TicketReopenedDomainEventHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketReopenedDomainEvent>
{
    public async Task Handle(TicketReopenedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var history = TicketHistory.Create(
            domainEvent.TicketId,
            domainEvent.ReopenedByUserId,
            TicketHistoryAction.Reopened,
            "Ticket reaberto",
            oldValue: null,
            newValue: null,
            dateTimeProvider.UtcNow);

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
