using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.Post;

internal sealed class TicketMessagePostedDomainEventHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketMessagePostedDomainEvent>
{
    public async Task Handle(TicketMessagePostedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        context.TicketHistories.Add(TicketHistoryEntries.Create(
            domainEvent.TicketId,
            domainEvent.AuthorUserId,
            TicketHistoryAction.MessageAdded,
            TicketHistoryDescriptions.MessagePosted,
            dateTimeProvider.UtcNow));

        await context.SaveChangesAsync(cancellationToken);
    }
}
