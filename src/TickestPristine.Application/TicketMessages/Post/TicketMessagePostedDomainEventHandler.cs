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

        var history = TicketHistory.Create(
            domainEvent.TicketId,
            message.AuthorUserId,
            TicketHistoryAction.MessageAdded,
            "Nova mensagem adicionada ao ticket",
            oldValue: null,
            newValue: null,
            dateTimeProvider.UtcNow);

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
