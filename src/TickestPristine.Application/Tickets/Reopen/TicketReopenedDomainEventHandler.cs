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
        context.TicketHistories.Add(TicketHistoryEntries.Create(
            domainEvent.TicketId,
            domainEvent.ReopenedByUserId,
            TicketHistoryAction.Reopened,
            TicketHistoryDescriptions.Reopened,
            dateTimeProvider.UtcNow));

        await context.SaveChangesAsync(cancellationToken);
    }
}
