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

        var history = TicketHistory.Create(
            domainEvent.TicketId,
            attachment.UploadedByUserId,
            TicketHistoryAction.AttachmentAdded,
            $"Anexo '{attachment.FileName}' adicionado",
            oldValue: null,
            newValue: null,
            dateTimeProvider.UtcNow);

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
