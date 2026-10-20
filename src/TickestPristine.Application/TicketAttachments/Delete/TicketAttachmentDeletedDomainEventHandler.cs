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
        // O anexo já foi excluído; o histórico usa apenas os dados do evento e o usuário atual.
        context.TicketHistories.Add(TicketHistoryEntries.Create(
            domainEvent.TicketId,
            userContext.UserId,
            TicketHistoryAction.AttachmentRemoved,
            TicketHistoryDescriptions.AttachmentRemoved,
            dateTimeProvider.UtcNow));

        await context.SaveChangesAsync(cancellationToken);
    }
}
