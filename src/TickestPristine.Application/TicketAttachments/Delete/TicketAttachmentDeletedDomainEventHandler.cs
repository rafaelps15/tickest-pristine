using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.Delete;

internal sealed class TicketAttachmentDeletedDomainEventHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketAttachmentDeletedDomainEvent>
{
    public async Task Handle(TicketAttachmentDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // The attachment row is already soft-deleted (and excluded by its query filter) by the time
        // this runs, so we don't re-query it - the event id plus the acting user is all we need.
        var history = TicketHistory.Create(
            domainEvent.TicketId,
            userContext.UserId,
            TicketHistoryAction.AttachmentRemoved,
            "Anexo removido",
            oldValue: null,
            newValue: null,
            dateTimeProvider.UtcNow);

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
