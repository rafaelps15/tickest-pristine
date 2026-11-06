using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.TicketMessages;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketMessages.TicketMessageEdited;

internal sealed class ProjectActivityOnTicketMessageEditedDomainEventHandler(
    ITicketActivityProjector activityProjector)
    : IDomainEventHandler<TicketMessageEditedDomainEvent>
{
    public Task Handle(TicketMessageEditedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        activityProjector.ProjectAsync(domainEvent.TicketId, cancellationToken);
}
