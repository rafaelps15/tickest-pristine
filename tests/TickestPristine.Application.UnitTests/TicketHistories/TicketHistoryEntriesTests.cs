using TickestPristine.Application.TicketHistories;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.TicketHistories;

public sealed class TicketHistoryEntriesTests
{
    private static readonly DateTime OccurredAtUtc = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Created_Should_RecordOpenStatusByCreator()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        // Act
        TicketHistory history = TicketHistoryEntries.Created(ticketId, creatorId, OccurredAtUtc);

        // Assert
        history.TicketId.ShouldBe(ticketId);
        history.ChangedByUserId.ShouldBe(creatorId);
        history.Action.ShouldBe(TicketHistoryAction.Created);
        history.OldValue.ShouldBeNull();
        history.NewValue.ShouldBe(nameof(TicketStatus.Open));
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
    }

    [Fact]
    public void StatusChanged_Should_RecordOldAndNewStatus()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        // Act
        TicketHistory history = TicketHistoryEntries.StatusChanged(
            ticketId,
            agentId,
            TicketStatus.Open,
            TicketStatus.InProgress,
            OccurredAtUtc);

        // Assert
        history.TicketId.ShouldBe(ticketId);
        history.ChangedByUserId.ShouldBe(agentId);
        history.Action.ShouldBe(TicketHistoryAction.StatusChanged);
        history.OldValue.ShouldBe(nameof(TicketStatus.Open));
        history.NewValue.ShouldBe(nameof(TicketStatus.InProgress));
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
    }

    [Fact]
    public void Unassigned_Should_RecordPreviousAssigneeWithoutNewValue()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var previousAssigneeId = Guid.NewGuid();

        // Act
        TicketHistory history = TicketHistoryEntries.Unassigned(ticketId, agentId, previousAssigneeId, OccurredAtUtc);

        // Assert
        history.TicketId.ShouldBe(ticketId);
        history.ChangedByUserId.ShouldBe(agentId);
        history.Action.ShouldBe(TicketHistoryAction.Unassigned);
        history.OldValue.ShouldBe(previousAssigneeId.ToString());
        history.NewValue.ShouldBeNull();
        history.OccurredAtUtc.ShouldBe(OccurredAtUtc);
    }
}
