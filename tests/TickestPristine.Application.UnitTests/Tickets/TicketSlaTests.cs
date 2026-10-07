using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class TicketSlaTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(TicketPriority.Urgent, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void DueAt_Should_AddResolutionHoursOfPriority(TicketPriority priority, int hours)
    {
        // Arrange
        DateTime expectedDueAtUtc = CreatedAtUtc.AddHours(hours);

        // Act
        DateTime dueAtUtc = TicketSla.DueAt(CreatedAtUtc, priority);

        // Assert
        dueAtUtc.ShouldBe(expectedDueAtUtc);
    }

    [Fact]
    public void WarningAt_Should_BeAtSeventyFivePercentOfWindow()
    {
        // Arrange
        DateTime expectedWarningAtUtc = CreatedAtUtc.AddHours(3);

        // Act
        DateTime warningAtUtc = TicketSla.WarningAt(CreatedAtUtc, TicketPriority.Urgent);

        // Assert
        warningAtUtc.ShouldBe(expectedWarningAtUtc);
    }

    [Theory]
    [InlineData(0, TicketSlaStatus.OnTime)]
    [InlineData(179, TicketSlaStatus.OnTime)]
    [InlineData(180, TicketSlaStatus.AtRisk)]
    [InlineData(239, TicketSlaStatus.AtRisk)]
    [InlineData(240, TicketSlaStatus.Breached)]
    [InlineData(600, TicketSlaStatus.Breached)]
    public void GetStatus_Should_FollowWindow_WhenTicketIsNotResolved(int minutesAfterCreation, TicketSlaStatus expected)
    {
        // Arrange
        DateTime dueAtUtc = TicketSla.DueAt(CreatedAtUtc, TicketPriority.Urgent);
        DateTime warningAtUtc = TicketSla.WarningAt(CreatedAtUtc, TicketPriority.Urgent);

        // Act
        TicketSlaStatus status = TicketSla.GetStatus(
            TicketStatus.InProgress,
            dueAtUtc,
            warningAtUtc,
            null,
            CreatedAtUtc.AddMinutes(minutesAfterCreation));

        // Assert
        status.ShouldBe(expected);
    }

    [Theory]
    [InlineData(240, TicketSlaStatus.Met)]
    [InlineData(241, TicketSlaStatus.Missed)]
    public void GetStatus_Should_FreezeResult_WhenTicketWasResolvedAndReopened(int resolvedMinutesAfterCreation, TicketSlaStatus expected)
    {
        // Arrange
        DateTime dueAtUtc = TicketSla.DueAt(CreatedAtUtc, TicketPriority.Urgent);
        DateTime warningAtUtc = TicketSla.WarningAt(CreatedAtUtc, TicketPriority.Urgent);

        // Act
        TicketSlaStatus status = TicketSla.GetStatus(
            TicketStatus.Open,
            dueAtUtc,
            warningAtUtc,
            CreatedAtUtc.AddMinutes(resolvedMinutesAfterCreation),
            CreatedAtUtc.AddDays(10));

        // Assert
        status.ShouldBe(expected);
    }

    [Fact]
    public void GetStatus_Should_ReturnNotApplicable_WhenTicketWasCanceledWithoutResolution()
    {
        // Arrange
        DateTime dueAtUtc = TicketSla.DueAt(CreatedAtUtc, TicketPriority.Urgent);
        DateTime warningAtUtc = TicketSla.WarningAt(CreatedAtUtc, TicketPriority.Urgent);

        // Act
        TicketSlaStatus status = TicketSla.GetStatus(
            TicketStatus.Canceled,
            dueAtUtc,
            warningAtUtc,
            null,
            CreatedAtUtc.AddDays(10));

        // Assert
        status.ShouldBe(TicketSlaStatus.NotApplicable);
    }
}
