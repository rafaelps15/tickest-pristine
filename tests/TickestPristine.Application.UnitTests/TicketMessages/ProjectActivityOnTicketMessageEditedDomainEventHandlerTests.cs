using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.TicketMessages.TicketMessageEdited;
using TickestPristine.Domain.TicketMessages;

namespace TickestPristine.Application.UnitTests.TicketMessages;

public sealed class ProjectActivityOnTicketMessageEditedDomainEventHandlerTests
{
    private static readonly Guid TicketId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ProjectTicketActivity_WhenMessageIsEdited()
    {
        // Arrange
        ITicketActivityProjector activityProjector = Substitute.For<ITicketActivityProjector>();
        var handler = new ProjectActivityOnTicketMessageEditedDomainEventHandler(activityProjector);

        // Act
        await handler.Handle(new TicketMessageEditedDomainEvent(Guid.NewGuid(), TicketId, Guid.NewGuid(), "Conteúdo anterior"), CancellationToken.None);

        // Assert
        await activityProjector.Received(1).ProjectAsync(TicketId, CancellationToken.None);
    }
}
