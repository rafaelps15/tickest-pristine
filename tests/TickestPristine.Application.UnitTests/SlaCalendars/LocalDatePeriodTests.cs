using TickestPristine.Application.SlaCalendars;

namespace TickestPristine.Application.UnitTests.SlaCalendars;

public sealed class LocalDatePeriodTests
{
    [Fact]
    public void FromLocalDates_Should_CoverWholeLocalDays_WhenBothDatesAreGiven()
    {
        // Act
        var period = LocalDatePeriod.FromLocalDates(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1));

        // Assert: 01/11 em São Paulo (UTC-3) vai de 03h00 UTC de 01/11 a 03h00 UTC de 02/11
        period.FromUtc.ShouldBe(new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc));
        period.FromUtc!.Value.Kind.ShouldBe(DateTimeKind.Utc);
        period.UntilUtc.ShouldBe(new DateTime(2026, 11, 2, 3, 0, 0, DateTimeKind.Utc));
        period.UntilUtc!.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void FromLocalDates_Should_LeaveLateEveningOfPreviousLocalDayOutside_WhenItIsNextDayInUtc()
    {
        // Arrange: 31/10 às 23h30 em São Paulo é 02h30 UTC de 01/11
        var lateEvening = new DateTime(2026, 11, 1, 2, 30, 0, DateTimeKind.Utc);
        var morning = new DateTime(2026, 11, 1, 13, 0, 0, DateTimeKind.Utc);

        // Act
        var period = LocalDatePeriod.FromLocalDates(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1));

        // Assert
        (lateEvening >= period.FromUtc).ShouldBeFalse();
        (morning >= period.FromUtc && morning < period.UntilUtc).ShouldBeTrue();
    }

    [Fact]
    public void FromLocalDates_Should_LeaveMissingEndOpen_WhenOnlyOneDateIsGiven()
    {
        // Act
        var onlyFrom = LocalDatePeriod.FromLocalDates(new DateOnly(2026, 11, 1), null);
        var onlyTo = LocalDatePeriod.FromLocalDates(null, new DateOnly(2026, 11, 1));

        // Assert
        onlyFrom.FromUtc.ShouldBe(new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc));
        onlyFrom.UntilUtc.ShouldBeNull();
        onlyTo.FromUtc.ShouldBeNull();
        onlyTo.UntilUtc.ShouldBe(new DateTime(2026, 11, 2, 3, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("2026-11-10", "2026-11-01", true)]
    [InlineData("2026-11-01", "2026-11-01", false)]
    [InlineData("2026-11-01", "2026-11-10", false)]
    [InlineData("2026-11-10", null, false)]
    [InlineData(null, "2026-11-01", false)]
    public void IsInverted_Should_BeTrueOnlyWhenStartIsAfterEnd(string? from, string? to, bool expected)
    {
        // Act
        bool inverted = LocalDatePeriod.IsInverted(
            from is null ? null : DateOnly.Parse(from, System.Globalization.CultureInfo.InvariantCulture),
            to is null ? null : DateOnly.Parse(to, System.Globalization.CultureInfo.InvariantCulture));

        // Assert
        inverted.ShouldBe(expected);
    }
}
