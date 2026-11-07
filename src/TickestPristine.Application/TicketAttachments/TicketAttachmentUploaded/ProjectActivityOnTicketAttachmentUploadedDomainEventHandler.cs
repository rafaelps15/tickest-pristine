using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.TicketAttachments;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.TicketAttachmentUploaded;

internal sealed class ProjectActivityOnTicketAttachmentUploadedDomainEventHandler(
    ITicketActivityProjector activityProjector)
    : IDomainEventHandler<TicketAttachmentUploadedDomainEvent>
{
    public Task Handle(TicketAttachmentUploadedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        activityProjector.ProjectAsync(domainEvent.TicketId, cancellationToken);
}
