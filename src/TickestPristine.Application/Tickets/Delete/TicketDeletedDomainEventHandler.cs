using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Delete;

internal sealed class TicketDeletedDomainEventHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketDeletedDomainEvent>
{
    public async Task Handle(TicketDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // O chamado já foi excluído; o histórico usa apenas os dados do evento e o usuário atual.
        var history = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = domainEvent.TicketId,
            ChangedByUserId = userContext.UserId,
            Action = TicketHistoryAction.Deleted,
            Description = "Chamado excluído",
            OldValue = null,
            NewValue = null,
            OccurredAtUtc = dateTimeProvider.UtcNow
        };

        history.Raise(new TicketHistoryRecordedDomainEvent(history.Id, history.TicketId));

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
