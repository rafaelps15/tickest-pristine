using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.Upload;

internal sealed class TicketAttachmentUploadedDomainEventHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketAttachmentUploadedDomainEvent>
{
    public async Task Handle(TicketAttachmentUploadedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        TicketAttachment? attachment = await context.TicketAttachments
            .SingleOrDefaultAsync(a => a.Id == domainEvent.AttachmentId, cancellationToken);

        if (attachment is null)
        {
            return;
        }

        var history = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = domainEvent.TicketId,
            ChangedByUserId = attachment.UploadedByUserId,
            Action = TicketHistoryAction.AttachmentAdded,
            Description = $"Anexo '{attachment.FileName}' adicionado",
            OldValue = null,
            NewValue = null,
            OccurredAtUtc = dateTimeProvider.UtcNow
        };

        history.Raise(new TicketHistoryRecordedDomainEvent(history.Id, history.TicketId));

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
