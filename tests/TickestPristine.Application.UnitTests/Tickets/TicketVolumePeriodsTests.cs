using TickestPristine.Application.Tickets.GetVolume;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class TicketVolumePeriodsTests
{
    // Quarta-feira, 28/10/2026.
    private static readonly DateOnly Today = new(2026, 10, 28);

    [Theory]
    [InlineData(TicketVolumeGrouping.Day, 30, "2026-09-29", "2026-10-28")]
    [InlineData(TicketVolumeGrouping.Week, 12, "2026-08-10", "2026-10-26")]
    [InlineData(TicketVolumeGrouping.Month, 12, "2025-11-01", "2026-10-01")]
    public void Build_Should_ReturnDefaultWindowEndingToday_WhenNoPeriodIsGiven(
        TicketVolumeGrouping groupBy,
        int expectedPoints,
        string expectedFirst,
        string expectedLast)
    {
        // Act
        Result<IReadOnlyList<DateOnly>> periods = TicketVolumePeriods.Build(groupBy, null, null, Today);

        // Assert: a semana começa na segunda-feira e o mês no dia 1º
        periods.IsSuccess.ShouldBeTrue();
        periods.Value.Count.ShouldBe(expectedPoints);
        periods.Value[0].ShouldBe(Parse(expectedFirst));
        periods.Value[^1].ShouldBe(Parse(expectedLast));
    }

    [Fact]
    public void Build_Should_CoverWholePeriodInWeeks_WhenPeriodIsGiven()
    {
        // Act: AC-4 da spec 028
        Result<IReadOnlyList<DateOnly>> periods = TicketVolumePeriods.Build(
            TicketVolumeGrouping.Week,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 10, 31),
            Today);

        // Assert: da segunda-feira de 01/09 (31/08) à de 31/10 (26/10), semana a semana
        periods.Value[0].ShouldBe(new DateOnly(2026, 8, 31));
        periods.Value[^1].ShouldBe(new DateOnly(2026, 10, 26));
        periods.Value.Count.ShouldBe(9);
        periods.Value.Zip(periods.Value.Skip(1)).ShouldAllBe(pair => pair.Second == pair.First.AddDays(7));
    }

    [Fact]
    public void Build_Should_UseTodayAsEnd_WhenOnlyStartIsGiven()
    {
        // Act
        Result<IReadOnlyList<DateOnly>> periods = TicketVolumePeriods.Build(TicketVolumeGrouping.Day, new DateOnly(2026, 10, 20), null, Today);

        // Assert
        periods.Value[0].ShouldBe(new DateOnly(2026, 10, 20));
        periods.Value[^1].ShouldBe(Today);
        periods.Value.Count.ShouldBe(9);
    }

    [Fact]
    public void Build_Should_UseDefaultWindowEndingAtEnd_WhenOnlyEndIsGiven()
    {
        // Act
        Result<IReadOnlyList<DateOnly>> periods = TicketVolumePeriods.Build(TicketVolumeGrouping.Day, null, new DateOnly(2026, 9, 30), Today);

        // Assert
        periods.Value.Count.ShouldBe(30);
        periods.Value[0].ShouldBe(new DateOnly(2026, 9, 1));
        periods.Value[^1].ShouldBe(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void Build_Should_AcceptExactlyTheMaximumOfPoints_AndRefuseOneMore()
    {
        // Act: 366 dias de 01/01/2024 (ano bissexto) a 31/12/2024, e um dia a mais
        Result<IReadOnlyList<DateOnly>> atLimit = TicketVolumePeriods.Build(
            TicketVolumeGrouping.Day, new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), Today);
        Result<IReadOnlyList<DateOnly>> overLimit = TicketVolumePeriods.Build(
            TicketVolumeGrouping.Day, new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1), Today);

        // Assert
        atLimit.Value.Count.ShouldBe(TicketVolumePeriods.MaxPoints);
        overLimit.IsFailure.ShouldBeTrue();
        overLimit.Error.ShouldBe(TicketErrors.VolumeTooManyPoints(TicketVolumePeriods.MaxPoints));
    }

    [Theory]
    [InlineData("2026-10-10", "2026-10-01")]
    [InlineData("2026-11-05", null)]
    public void Build_Should_ReturnInvalidPeriod_WhenStartIsAfterEnd(string from, string? to)
    {
        // Act: início depois do fim informado, ou depois de hoje quando o fim não vem
        Result<IReadOnlyList<DateOnly>> periods = TicketVolumePeriods.Build(
            TicketVolumeGrouping.Day,
            Parse(from),
            to is null ? null : Parse(to),
            Today);

        // Assert
        periods.IsFailure.ShouldBeTrue();
        periods.Error.ShouldBe(TicketErrors.InvalidPeriod);
    }

    private static DateOnly Parse(string date) => DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture);
}
