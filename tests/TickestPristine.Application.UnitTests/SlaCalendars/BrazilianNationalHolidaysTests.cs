using TickestPristine.Application.SlaCalendars;

namespace TickestPristine.Application.UnitTests.SlaCalendars;

public sealed class BrazilianNationalHolidaysTests
{
    private const string GoodFriday = "Sexta-feira Santa";

    // A Sexta-feira Santa é a Páscoa menos dois dias: conferi-la confere o cálculo da Páscoa.
    [Theory]
    [InlineData(2024, 3, 29)]
    [InlineData(2025, 4, 18)]
    [InlineData(2026, 4, 3)]
    [InlineData(2027, 3, 26)]
    [InlineData(2028, 4, 14)]
    [InlineData(1818, 3, 20)] // Páscoa mais cedo possível: 22/03.
    [InlineData(2038, 4, 23)] // Páscoa mais tarde possível: 25/04.
    public void For_Should_ReturnGoodFridayTwoDaysBeforeEaster_WhenYearIsGiven(int year, int month, int day)
    {
        // Act
        IReadOnlyList<BrazilianHoliday> holidays = BrazilianNationalHolidays.For(year);

        // Assert
        BrazilianHoliday goodFriday = holidays.Single(h => h.Name == GoodFriday);
        goodFriday.Date.ShouldBe(new DateOnly(year, month, day));
        goodFriday.Kind.ShouldBe(HolidayKind.National);
    }

    [Fact]
    public void For_Should_ClassifyCarnivalAndCorpusChristiAsOptionalDayOff_WhenYearIs2026()
    {
        // Act
        IReadOnlyList<BrazilianHoliday> holidays = BrazilianNationalHolidays.For(2026);

        // Assert
        holidays.Where(h => h.Kind == HolidayKind.OptionalDayOff)
            .Select(h => (h.Date, h.Name))
            .ShouldBe(
            [
                (new DateOnly(2026, 2, 16), "Carnaval (segunda-feira)"),
                (new DateOnly(2026, 2, 17), "Carnaval (terça-feira)"),
                (new DateOnly(2026, 6, 4), "Corpus Christi")
            ]);
    }

    [Fact]
    public void For_Should_ReturnThirteenDistinctDatesInAscendingOrder_WhenYearIs2026()
    {
        // Act
        IReadOnlyList<BrazilianHoliday> holidays = BrazilianNationalHolidays.For(2026);

        // Assert
        holidays.Count.ShouldBe(13);
        holidays.Select(h => h.Date).ShouldBe(holidays.Select(h => h.Date).Order());
        holidays.Select(h => h.Date).Distinct().Count().ShouldBe(13);
        holidays.ShouldAllBe(h => !string.IsNullOrWhiteSpace(h.Name));
    }

    [Fact]
    public void For_Should_ReturnExpectedDates_WhenYearIs2026()
    {
        // Act (AC-12)
        IReadOnlyList<BrazilianHoliday> holidays = BrazilianNationalHolidays.For(2026);

        // Assert
        holidays.Select(h => h.Date).ShouldBe(
        [
            new(2026, 1, 1), new(2026, 2, 16), new(2026, 2, 17), new(2026, 4, 3), new(2026, 4, 21),
            new(2026, 5, 1), new(2026, 6, 4), new(2026, 9, 7), new(2026, 10, 12), new(2026, 11, 2),
            new(2026, 11, 15), new(2026, 11, 20), new(2026, 12, 25)
        ]);
    }

    [Fact]
    public void For_Should_ReturnMovableHolidaysFromEaster_WhenYearIs2027()
    {
        // Act (AC-13)
        IReadOnlyList<BrazilianHoliday> holidays = BrazilianNationalHolidays.For(2027);

        // Assert
        holidays.Count.ShouldBe(13);
        DateOf(holidays, "Carnaval (segunda-feira)").ShouldBe(new DateOnly(2027, 2, 8));
        DateOf(holidays, "Carnaval (terça-feira)").ShouldBe(new DateOnly(2027, 2, 9));
        DateOf(holidays, GoodFriday).ShouldBe(new DateOnly(2027, 3, 26));
        DateOf(holidays, "Corpus Christi").ShouldBe(new DateOnly(2027, 5, 27));
    }

    [Theory]
    [InlineData(2023, false, 12)]
    [InlineData(2024, true, 13)]
    [InlineData(2026, true, 13)]
    public void For_Should_IncludeBlackConsciousnessDayAsNationalOnlyFrom2024_WhenYearIsGiven(
        int year,
        bool expectedHoliday,
        int expectedCount)
    {
        // Act
        IReadOnlyList<BrazilianHoliday> holidays = BrazilianNationalHolidays.For(year);

        // Assert
        holidays.Any(h => h.Date == new DateOnly(year, 11, 20) && h.Kind == HolidayKind.National)
            .ShouldBe(expectedHoliday);
        holidays.Count.ShouldBe(expectedCount);
    }

    [Fact]
    public void For_Should_OrderGoodFridayAfterTiradentes_WhenEasterIsOnApril25()
    {
        // Act
        List<string> names = [.. BrazilianNationalHolidays.For(2038).Select(h => h.Name)];

        // Assert
        names.IndexOf("Tiradentes").ShouldBeLessThan(names.IndexOf(GoodFriday));
    }

    [Fact]
    public void For_Should_ReturnSortedDatesOnExpectedWeekdays_WhenYearIsInSupportedRange()
    {
        for (int year = 1583; year <= 9999; year++)
        {
            // Act
            IReadOnlyList<BrazilianHoliday> holidays = BrazilianNationalHolidays.For(year);

            // Assert
            holidays.Select(h => h.Date).ShouldBe(holidays.Select(h => h.Date).Order(), $"ano {year}");
            holidays.ShouldAllBe(h => h.Date.Year == year, $"ano {year}");
            DayOf(holidays, "Carnaval (segunda-feira)").ShouldBe(DayOfWeek.Monday, $"ano {year}");
            DayOf(holidays, "Carnaval (terça-feira)").ShouldBe(DayOfWeek.Tuesday, $"ano {year}");
            DayOf(holidays, GoodFriday).ShouldBe(DayOfWeek.Friday, $"ano {year}");
            DayOf(holidays, "Corpus Christi").ShouldBe(DayOfWeek.Thursday, $"ano {year}");
        }
    }

    [Theory]
    [InlineData(1582)]
    [InlineData(10000)]
    public void For_Should_ThrowArgumentOutOfRange_WhenYearIsOutsideSupportedRange(int year)
    {
        // Act
        Action act = () => BrazilianNationalHolidays.For(year);

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    private static DayOfWeek DayOf(IReadOnlyList<BrazilianHoliday> holidays, string name) =>
        DateOf(holidays, name).DayOfWeek;

    private static DateOnly DateOf(IReadOnlyList<BrazilianHoliday> holidays, string name) =>
        holidays.Single(h => h.Name == name).Date;
}
