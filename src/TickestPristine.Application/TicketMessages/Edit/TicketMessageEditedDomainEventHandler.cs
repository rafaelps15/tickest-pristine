using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.Edit;

internal sealed class TicketMessageEditedDomainEventHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketMessageEditedDomainEvent>
{
    public async Task Handle(TicketMessageEditedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        TicketMessage? message = await context.TicketMessages
            .SingleOrDefaultAsync(m => m.Id == domainEvent.MessageId, cancellationToken);

        if (message is null)
        {
            return;
        }

        var history = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = domainEvent.TicketId,
            ChangedByUserId = message.AuthorUserId,
            Action = TicketHistoryAction.MessageEdited,
            Description = "Mensagem editada",
            OldValue = null,
            NewValue = null,
            OccurredAtUtc = dateTimeProvider.UtcNow
        };

        history.Raise(new TicketHistoryRecordedDomainEvent(history.Id, history.TicketId));

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
