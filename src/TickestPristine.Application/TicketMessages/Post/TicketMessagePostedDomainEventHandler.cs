using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.Post;

internal sealed class TicketMessagePostedDomainEventHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketMessagePostedDomainEvent>
{
    public async Task Handle(TicketMessagePostedDomainEvent domainEvent, CancellationToken cancellationToken)
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
            Action = TicketHistoryAction.MessageAdded,
            Description = "Nova mensagem adicionada ao chamado",
            OldValue = null,
            NewValue = null,
            OccurredAtUtc = dateTimeProvider.UtcNow
        };

        history.Raise(new TicketHistoryRecordedDomainEvent(history.Id, history.TicketId));

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
