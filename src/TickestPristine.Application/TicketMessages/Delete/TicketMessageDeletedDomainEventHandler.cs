using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.Delete;

internal sealed class TicketMessageDeletedDomainEventHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketMessageDeletedDomainEvent>
{
    public async Task Handle(TicketMessageDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // The message row still exists (soft-deleted, filtered out of normal reads) but we don't
        // need to touch it - the acting user comes from the current request, not the message.
        var history = TicketHistory.Create(
            domainEvent.TicketId,
            userContext.UserId,
            TicketHistoryAction.MessageRemoved,
            "Mensagem removida",
            oldValue: null,
            newValue: null,
            dateTimeProvider.UtcNow);

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
