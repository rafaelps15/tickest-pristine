using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.TicketMessages;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.TicketMessagePosted;

internal sealed class ProjectActivityOnTicketMessagePostedDomainEventHandler(
    ITicketActivityProjector activityProjector)
    : IDomainEventHandler<TicketMessagePostedDomainEvent>
{
    public Task Handle(TicketMessagePostedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        activityProjector.ProjectAsync(domainEvent.TicketId, cancellationToken);
}
