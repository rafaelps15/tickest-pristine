using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.Update;

internal sealed class TicketStatusChangedDomainEventHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketStatusChangedDomainEvent>
{
    public async Task Handle(TicketStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var history = new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = domainEvent.TicketId,
            ChangedByUserId = domainEvent.ChangedByUserId,
            Action = TicketHistoryAction.StatusChanged,
            Description = $"Status alterado de {TicketStatusNames.GetDisplayName(domainEvent.OldStatus)} para {TicketStatusNames.GetDisplayName(domainEvent.NewStatus)}",
            OldValue = domainEvent.OldStatus.ToString(),
            NewValue = domainEvent.NewStatus.ToString(),
            OccurredAtUtc = dateTimeProvider.UtcNow
        };

        history.Raise(new TicketHistoryRecordedDomainEvent(history.Id, history.TicketId));

        context.TicketHistories.Add(history);

        await context.SaveChangesAsync(cancellationToken);
    }
}
