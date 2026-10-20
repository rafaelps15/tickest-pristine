using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.Edit;

internal sealed class TicketMessageEditedDomainEventHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketMessageEditedDomainEvent>
{
    public async Task Handle(TicketMessageEditedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        context.TicketHistories.Add(TicketHistoryEntries.Create(
            domainEvent.TicketId,
            domainEvent.AuthorUserId,
            TicketHistoryAction.MessageEdited,
            TicketHistoryDescriptions.MessageEdited,
            dateTimeProvider.UtcNow));

        await context.SaveChangesAsync(cancellationToken);
    }
}
