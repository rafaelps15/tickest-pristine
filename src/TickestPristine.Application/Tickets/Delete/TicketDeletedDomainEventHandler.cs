using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Delete;

internal sealed class TicketDeletedDomainEventHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketDeletedDomainEvent>
{
    public async Task Handle(TicketDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // The ticket is already soft-deleted (and excluded by its query filter) by the time this
        // runs, so we don't re-query it - the event id plus the acting user is all we need.
        var history = TicketHistory.Create(
            domainEvent.TicketId,
            userContext.UserId,
            TicketHistoryAction.Deleted,
            "Ticket excluído",
            oldValue: null,
            newValue: null,
            dateTimeProvider.UtcNow);

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
