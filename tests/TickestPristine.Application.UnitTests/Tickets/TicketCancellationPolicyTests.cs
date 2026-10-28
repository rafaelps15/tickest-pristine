using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class TicketCancellationPolicyTests
{
    private static readonly DateTime OpenedAtUtc = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OwnerDeadline_Should_BeFifteenMinutesAfterOpening()
    {
        // Act
        DateTime deadline = TicketCancellationPolicy.OwnerDeadline(OpenedAtUtc);

        // Assert
        TicketCancellationPolicy.OwnerWindowMinutes.ShouldBe(15);
        deadline.ShouldBe(OpenedAtUtc.AddMinutes(15));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(14, true)]
    [InlineData(15, true)]
    [InlineData(16, false)]
    public void IsWithinOwnerWindow_Should_IncludeTheDeadline_WhenMinutesAfterOpeningAreGiven(int minutes, bool expected)
    {
        // Act
        bool withinWindow = TicketCancellationPolicy.IsWithinOwnerWindow(OpenedAtUtc, OpenedAtUtc.AddMinutes(minutes));

        // Assert
        withinWindow.ShouldBe(expected);
    }

    [Theory]
    [InlineData(TicketStatus.Open, 10, false, true, true)]
    [InlineData(TicketStatus.InProgress, 10, false, true, true)]
    [InlineData(TicketStatus.Open, 20, false, true, false)]
    [InlineData(TicketStatus.Open, 20, true, false, true)]
    [InlineData(TicketStatus.Open, 10, false, false, false)]
    [InlineData(TicketStatus.Resolved, 10, true, true, false)]
    [InlineData(TicketStatus.Closed, 10, true, true, false)]
    [InlineData(TicketStatus.Canceled, 10, true, true, false)]
    public void CanCancel_Should_RequireActiveTicketAndPermissionOrOwnerWindow_WhenEvaluated(
        TicketStatus status,
        int minutesAfterOpening,
        bool canCancelAny,
        bool isOwnerWhoCanEdit,
        bool expected)
    {
        // Act
        bool canCancel = TicketCancellationPolicy.CanCancel(
            status,
            OpenedAtUtc,
            OpenedAtUtc.AddMinutes(minutesAfterOpening),
            canCancelAny,
            isOwnerWhoCanEdit);

        // Assert
        canCancel.ShouldBe(expected);
    }
}
