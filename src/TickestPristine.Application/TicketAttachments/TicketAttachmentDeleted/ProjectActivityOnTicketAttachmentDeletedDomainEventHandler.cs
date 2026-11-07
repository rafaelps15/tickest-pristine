using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.TicketAttachments;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.TicketAttachmentDeleted;

internal sealed class ProjectActivityOnTicketAttachmentDeletedDomainEventHandler(
    ITicketActivityProjector activityProjector)
    : IDomainEventHandler<TicketAttachmentDeletedDomainEvent>
{
    public Task Handle(TicketAttachmentDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        activityProjector.ProjectAsync(domainEvent.TicketId, cancellationToken);
}
