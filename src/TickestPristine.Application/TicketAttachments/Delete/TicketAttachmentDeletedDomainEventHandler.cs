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
        var history = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = domainEvent.TicketId,
            ChangedByUserId = userContext.UserId,
            Action = TicketHistoryAction.AttachmentRemoved,
            Description = "Anexo removido",
            OldValue = null,
            NewValue = null,
            OccurredAtUtc = dateTimeProvider.UtcNow
        };

        history.Raise(new TicketHistoryRecordedDomainEvent(history.Id, history.TicketId));

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
