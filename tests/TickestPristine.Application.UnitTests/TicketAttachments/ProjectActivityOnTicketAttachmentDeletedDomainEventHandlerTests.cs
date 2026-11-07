using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.TicketAttachments.TicketAttachmentDeleted;
using TickestPristine.Domain.TicketAttachments;

namespace TickestPristine.Application.UnitTests.TicketAttachments;

public sealed class ProjectActivityOnTicketAttachmentDeletedDomainEventHandlerTests
{
    private static readonly Guid TicketId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ProjectTicketActivity_WhenAttachmentIsDeleted()
    {
        // Arrange
        ITicketActivityProjector activityProjector = Substitute.For<ITicketActivityProjector>();
        var handler = new ProjectActivityOnTicketAttachmentDeletedDomainEventHandler(activityProjector);

        // Act
        await handler.Handle(new TicketAttachmentDeletedDomainEvent(Guid.NewGuid(), TicketId), CancellationToken.None);

        // Assert
        await activityProjector.Received(1).ProjectAsync(TicketId, CancellationToken.None);
    }
}
