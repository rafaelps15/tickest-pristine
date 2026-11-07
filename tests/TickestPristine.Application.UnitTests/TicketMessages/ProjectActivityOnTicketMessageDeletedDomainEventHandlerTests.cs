using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.TicketMessages.TicketMessageDeleted;
using TickestPristine.Domain.TicketMessages;

namespace TickestPristine.Application.UnitTests.TicketMessages;

public sealed class ProjectActivityOnTicketMessageDeletedDomainEventHandlerTests
{
    private static readonly Guid TicketId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ProjectTicketActivity_WhenMessageIsDeleted()
    {
        // Arrange
        ITicketActivityProjector activityProjector = Substitute.For<ITicketActivityProjector>();
        var handler = new ProjectActivityOnTicketMessageDeletedDomainEventHandler(activityProjector);

        // Act
        await handler.Handle(new TicketMessageDeletedDomainEvent(Guid.NewGuid(), TicketId), CancellationToken.None);

        // Assert
        await activityProjector.Received(1).ProjectAsync(TicketId, CancellationToken.None);
    }
}
