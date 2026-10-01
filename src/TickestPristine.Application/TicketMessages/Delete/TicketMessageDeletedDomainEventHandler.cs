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
        // A mensagem já foi excluída; o histórico usa apenas os dados do evento e o usuário atual.
        var history = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = domainEvent.TicketId,
            ChangedByUserId = userContext.UserId,
            Action = TicketHistoryAction.MessageRemoved,
            Description = "Mensagem removida",
            OldValue = null,
            NewValue = null,
            OccurredAtUtc = dateTimeProvider.UtcNow
        };

        history.Raise(new TicketHistoryRecordedDomainEvent(history.Id, history.TicketId));

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
