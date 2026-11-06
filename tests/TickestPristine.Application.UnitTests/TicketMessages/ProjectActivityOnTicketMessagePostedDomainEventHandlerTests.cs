using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.TicketMessages.TicketMessagePosted;
using TickestPristine.Domain.TicketMessages;

namespace TickestPristine.Application.UnitTests.TicketMessages;

public sealed class ProjectActivityOnTicketMessagePostedDomainEventHandlerTests
{
    private static readonly Guid TicketId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ProjectTicketActivity_WhenMessageIsPosted()
    {
        // Arrange
        ITicketActivityProjector activityProjector = Substitute.For<ITicketActivityProjector>();
        var handler = new ProjectActivityOnTicketMessagePostedDomainEventHandler(activityProjector);

        // Act
        await handler.Handle(new TicketMessagePostedDomainEvent(Guid.NewGuid(), TicketId, Guid.NewGuid()), CancellationToken.None);

        // Assert
        await activityProjector.Received(1).ProjectAsync(TicketId, CancellationToken.None);
    }
}
