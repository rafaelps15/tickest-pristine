using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.Upload;

internal sealed class TicketAttachmentUploadedDomainEventHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketAttachmentUploadedDomainEvent>
{
    public async Task Handle(TicketAttachmentUploadedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        context.TicketHistories.Add(TicketHistoryEntries.Create(
            domainEvent.TicketId,
            domainEvent.UploadedByUserId,
            TicketHistoryAction.AttachmentAdded,
            TicketHistoryDescriptions.AttachmentAdded(domainEvent.FileName),
            dateTimeProvider.UtcNow));

        await context.SaveChangesAsync(cancellationToken);
    }
}
