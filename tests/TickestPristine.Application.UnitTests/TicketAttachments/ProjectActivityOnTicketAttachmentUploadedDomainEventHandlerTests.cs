using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.TicketAttachments.TicketAttachmentUploaded;
using TickestPristine.Domain.TicketAttachments;

namespace TickestPristine.Application.UnitTests.TicketAttachments;

public sealed class ProjectActivityOnTicketAttachmentUploadedDomainEventHandlerTests
{
    private static readonly Guid TicketId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ProjectTicketActivity_WhenAttachmentIsUploaded()
    {
        // Arrange
        ITicketActivityProjector activityProjector = Substitute.For<ITicketActivityProjector>();
        var handler = new ProjectActivityOnTicketAttachmentUploadedDomainEventHandler(activityProjector);

        // Act
        await handler.Handle(new TicketAttachmentUploadedDomainEvent(Guid.NewGuid(), TicketId, Guid.NewGuid(), "relatorio.pdf"), CancellationToken.None);

        // Assert
        await activityProjector.Received(1).ProjectAsync(TicketId, CancellationToken.None);
    }
}
