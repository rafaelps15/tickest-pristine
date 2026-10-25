using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.UnitTests.SlaCalendars;

/// <summary>
/// Critérios da spec 004 em hora local de São Paulo; o <see cref="At"/> converte para UTC na entrada e o
/// <see cref="Local"/> converte de volta na saída.
/// </summary>
public sealed class BusinessCalendarTests
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private static readonly SlaWorkPeriod[] WeekdaysEightToSix =
    [
        .. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
            .Select(day => new SlaWorkPeriod(day, new TimeOnly(8, 0), new TimeOnly(18, 0)))
    ];

    [Theory]
    [InlineData("2026-10-16 17:59", 30, "2026-10-19 08:29")] // AC-1: sexta para segunda.
    [InlineData("2026-10-09 17:59", 30, "2026-10-13 08:29")] // AC-2: pula o feriado de 12/10.
    [InlineData("2026-10-16 17:00", 240, "2026-10-19 11:00")] // AC-3
    [InlineData("2026-10-17 10:00", 120, "2026-10-19 10:00")] // AC-4: início no sábado.
    [InlineData("2026-11-19 14:00", 1200, "2026-11-24 14:00")] // AC-5: pula o feriado de 20/11 e o fim de semana.
    [InlineData("2026-10-16 17:00", 60, "2026-10-16 18:00")] // AC-6: termina no fim da faixa (DEC-9).
    public void AddBusinessTime_Should_ReturnInstantWhenBusinessTimeRunsOut_WhenCalendarIsDefault(
        string start,
        int minutes,
        string expected)
    {
        // Arrange
        BusinessCalendar calendar = DefaultCalendar();

        // Act
        DateTime result = calendar.AddBusinessTime(At(start), TimeSpan.FromMinutes(minutes));

        // Assert
        Local(result).ShouldBe(Parse(expected));
        result.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void AddBusinessTime_Should_KeepSecondsPrecision_WhenDurationHasFractionOfMinute()
    {
        // Arrange (AC-21: alerta de 75% de 30 minutos na spec 005)
        BusinessCalendar calendar = DefaultCalendar();

        // Act
        DateTime result = calendar.AddBusinessTime(At("2026-10-26 08:10"), new TimeSpan(0, 22, 30));

        // Assert
        Local(result).ShouldBe(Parse("2026-10-26 08:32").AddSeconds(30));
    }

    [Theory]
    [InlineData("2026-10-19 08:10", 30, "2026-10-16 17:40")] // AC-7: segunda para sexta.
    [InlineData("2026-10-19 08:10", 10, "2026-10-19 08:00")] // AC-8: termina no início da faixa (DEC-9).
    public void SubtractBusinessTime_Should_ReturnInstantBusinessTimeBefore_WhenCalendarIsDefault(
        string end,
        int minutes,
        string expected)
    {
        // Arrange
        BusinessCalendar calendar = DefaultCalendar();

        // Act
        DateTime result = calendar.SubtractBusinessTime(At(end), TimeSpan.FromMinutes(minutes));

        // Assert
        Local(result).ShouldBe(Parse(expected));
    }

    [Fact]
    public void BusinessTimeBetween_Should_CountOnlyWorkPeriods_WhenCalendarIsDefault()
    {
        // Arrange (AC-9)
        BusinessCalendar calendar = DefaultCalendar();

        // Act
        TimeSpan forward = calendar.BusinessTimeBetween(At("2026-10-16 17:00"), At("2026-10-19 09:00"));
        TimeSpan backward = calendar.BusinessTimeBetween(At("2026-10-19 09:00"), At("2026-10-16 17:00"));

        // Assert
        forward.ShouldBe(TimeSpan.FromHours(2));
        forward.TotalSeconds.ShouldBe(7200);
        backward.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Operations_Should_UseElapsedTimeIgnoringHolidays_WhenCalendarIs24x7()
    {
        // Arrange (AC-10)
        var calendar = new BusinessCalendar([], SaoPaulo, is24x7: true, _ => new HashSet<DateOnly> { new(2026, 10, 17) });

        // Act
        DateTime added = calendar.AddBusinessTime(At("2026-10-16 22:00"), TimeSpan.FromMinutes(240));
        DateTime subtracted = calendar.SubtractBusinessTime(At("2026-10-17 02:00"), TimeSpan.FromMinutes(240));
        TimeSpan between = calendar.BusinessTimeBetween(At("2026-10-16 22:00"), At("2026-10-17 02:00"));

        // Assert
        Local(added).ShouldBe(Parse("2026-10-17 02:00"));
        Local(subtracted).ShouldBe(Parse("2026-10-16 22:00"));
        between.ShouldBe(TimeSpan.FromHours(4));
    }

    [Fact]
    public void Operations_Should_ThrowForAddAndSubtractAndReturnZeroBetween_WhenCalendarHasNoWorkPeriods()
    {
        // Arrange (AC-11)
        var calendar = new BusinessCalendar([], SaoPaulo, is24x7: false, _ => new HashSet<DateOnly>());

        // Act
        Action add = () => calendar.AddBusinessTime(At("2026-10-16 10:00"), TimeSpan.FromMinutes(1));
        Action subtract = () => calendar.SubtractBusinessTime(At("2026-10-16 10:00"), TimeSpan.FromMinutes(1));
        TimeSpan between = calendar.BusinessTimeBetween(At("2026-10-16 08:00"), At("2026-10-23 08:00"));

        // Assert
        add.ShouldThrow<InvalidOperationException>();
        subtract.ShouldThrow<InvalidOperationException>();
        between.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void BusinessTimeBetween_Should_CountIntervalLongerThanSearchLimit_WhenCalendarHasNoHolidays()
    {
        // Arrange (AC-22: 399 dias, 57 semanas de 50 horas)
        var calendar = new BusinessCalendar(WeekdaysEightToSix, SaoPaulo, is24x7: false, _ => new HashSet<DateOnly>());

        // Act
        TimeSpan between = calendar.BusinessTimeBetween(At("2026-10-19 08:00"), At("2027-11-22 08:00"));

        // Assert
        between.ShouldBe(TimeSpan.FromHours(2850));
    }

    [Fact]
    public void AddAndSubtract_Should_ReturnSameInstantForZeroAndThrowForNegative_WhenDurationIsNotPositive()
    {
        // Arrange (AC-23)
        BusinessCalendar calendar = DefaultCalendar();
        DateTime saturday = At("2026-10-17 10:00");

        // Act
        DateTime addedZero = calendar.AddBusinessTime(saturday, TimeSpan.Zero);
        Action addNegative = () => calendar.AddBusinessTime(saturday, TimeSpan.FromMinutes(-1));
        Action subtractNegative = () => calendar.SubtractBusinessTime(saturday, TimeSpan.FromMinutes(-1));

        // Assert
        addedZero.ShouldBe(saturday);
        addNegative.ShouldThrow<ArgumentOutOfRangeException>();
        subtractNegative.ShouldThrow<ArgumentOutOfRangeException>();
    }

    // Grade "Expediente" com os feriados nacionais, a mesma do calendário padrão do REQ-2.
    private static BusinessCalendar DefaultCalendar() => new(
        WeekdaysEightToSix,
        SaoPaulo,
        is24x7: false,
        year => BrazilianNationalHolidays.For(year).Select(h => h.Date).ToHashSet());

    private static DateTime Parse(string local) =>
        DateTime.ParseExact(local, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static DateTime At(string local) => TimeZoneInfo.ConvertTimeToUtc(Parse(local), SaoPaulo);

    private static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, SaoPaulo);
}
