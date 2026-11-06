using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.TicketMessages;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.TicketMessageDeleted;

internal sealed class ProjectActivityOnTicketMessageDeletedDomainEventHandler(
    ITicketActivityProjector activityProjector)
    : IDomainEventHandler<TicketMessageDeletedDomainEvent>
{
    public Task Handle(TicketMessageDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        activityProjector.ProjectAsync(domainEvent.TicketId, cancellationToken);
}
