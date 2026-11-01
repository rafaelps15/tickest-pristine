using System.Globalization;
using TickestPristine.Application.Abstractions.Sla;
using TickestPristine.Domain.Audit;
using TickestPristine.Domain.SlaCalendars;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Infrastructure.Database;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.SlaCalendars;

/// <summary>
/// Calendários de SLA no banco e no provider, pelo <c>DbContext</c> e pelo <see cref="ISlaCalendarProvider"/>
/// resolvidos do <c>Services</c>, sem HTTP (a spec 004 não tem rota). Horários em hora local de São Paulo.
/// </summary>
public sealed class SlaCalendarsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    [Fact]
    public async Task Migration_Should_CreateTablesWithoutData_WhenDatabaseIsMigrated()
    {
        // Arrange (AC-14)
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IMigrationsAssembly migrationsAssembly = context.GetService<IMigrationsAssembly>();
        string migrationId = migrationsAssembly.Migrations.Keys.Single(id => id.EndsWith("_Add_Sla_Calendars", StringComparison.Ordinal));

        // Act
        IEnumerable<string> applied = await context.Database.GetAppliedMigrationsAsync();
        Migration migration = migrationsAssembly.CreateMigration(
            migrationsAssembly.Migrations[migrationId],
            context.Database.ProviderName!);
        int calendarCount = await context.SlaCalendars.CountAsync();
        int holidayCount = await context.SlaCalendarHolidays.CountAsync();
        bool defaultInDatabase = await context.SlaCalendars.AnyAsync(c => c.Id == SlaCalendarIds.Default);

        // Assert
        applied.ShouldContain(migrationId);
        migration.UpOperations.OfType<InsertDataOperation>().ShouldBeEmpty();
        calendarCount.ShouldBeGreaterThanOrEqualTo(0);
        holidayCount.ShouldBeGreaterThanOrEqualTo(0);
        defaultInDatabase.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveChanges_Should_RejectSecondHolidayOnSameDate_WhenCalendarIsTheSame()
    {
        // Arrange (AC-15)
        SlaCalendar calendar = await AddCalendarAsync(SaturdayMorning());
        SlaCalendar otherCalendar = await AddCalendarAsync(SaturdayMorning());
        var date = new DateOnly(2026, 10, 24);
        await AddHolidayAsync(calendar.Id, date);

        // Act
        Func<Task> duplicate = () => AddHolidayAsync(calendar.Id, date);
        Func<Task> otherCalendarSameDate = () => AddHolidayAsync(otherCalendar.Id, date);

        // Assert
        DbUpdateException exception = await duplicate.ShouldThrowAsync<DbUpdateException>();
        exception.InnerException.ShouldBeOfType<Npgsql.PostgresException>().SqlState.ShouldBe("23505");
        await otherCalendarSameDate();
    }

    [Fact]
    public async Task GetAsync_Should_ReturnDefaultCalendarFromCode_WhenIdIsDefault()
    {
        // Arrange (AC-16)
        using IServiceScope scope = Services.CreateScope();
        ISlaCalendarProvider provider = scope.ServiceProvider.GetRequiredService<ISlaCalendarProvider>();

        // Act
        Result<BusinessCalendar> result = await provider.GetAsync(SlaCalendarIds.Default);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Local(result.Value.AddBusinessTime(At("2026-10-09 17:59"), TimeSpan.FromMinutes(30))).ShouldBe(Parse("2026-10-13 08:29"));
        Local(result.Value.AddBusinessTime(At("2030-12-24 17:59"), TimeSpan.FromMinutes(30))).ShouldBe(Parse("2030-12-26 08:29"));
    }

    [Fact]
    public async Task GetAsync_Should_ReturnNotFound_WhenCalendarDoesNotExist()
    {
        // Arrange (AC-17)
        using IServiceScope scope = Services.CreateScope();
        ISlaCalendarProvider provider = scope.ServiceProvider.GetRequiredService<ISlaCalendarProvider>();
        var calendarId = Guid.NewGuid();

        // Act
        Result<BusinessCalendar> result = await provider.GetAsync(calendarId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("SlaCalendars.NotFound");
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Description.ShouldBe($"O calendário de SLA com o Id = '{calendarId}' não foi encontrado");
    }

    [Fact]
    public async Task GetAsync_Should_KeepCachedCalendarUntilInvalidated_WhenDatabaseChanges()
    {
        // Arrange (AC-18)
        SlaCalendar calendar = await AddCalendarAsync(SaturdayMorning());
        DateTime start = At("2026-10-17 11:30");
        var oneHour = TimeSpan.FromMinutes(60);

        // Act
        DateTime first = await AddWithProviderAsync(calendar.Id, start, oneHour);
        await AddHolidayAsync(calendar.Id, new DateOnly(2026, 10, 24));
        DateTime cached = await AddWithProviderAsync(calendar.Id, start, oneHour);
        using (IServiceScope scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISlaCalendarProvider>().InvalidateAsync(calendar.Id);
        }

        DateTime refreshed = await AddWithProviderAsync(calendar.Id, start, oneHour);

        // Assert
        Local(first).ShouldBe(Parse("2026-10-24 08:30"));
        Local(cached).ShouldBe(Parse("2026-10-24 08:30"));
        Local(refreshed).ShouldBe(Parse("2026-10-31 08:30"));
    }

    [Fact]
    public async Task SaveChanges_Should_AuditCalendarAndHolidayChanges_WhenCalendarIsIncludedAndChanged()
    {
        // Arrange (AC-19)
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        SlaCalendar calendar = NewCalendar(SaturdayMorning());
        var holiday = new SlaCalendarHoliday
        {
            Id = Guid.NewGuid(),
            SlaCalendarId = calendar.Id,
            Date = new DateOnly(2026, 12, 26),
            Description = "Recesso"
        };

        // Act
        context.SlaCalendars.Add(calendar);
        context.SlaCalendarHolidays.Add(holiday);
        await context.SaveChangesAsync();

        calendar.Name = "Plantão de sábado (revisado)";
        await context.SaveChangesAsync();

        calendar.WorkSchedule.Add(new SlaWorkPeriod(DayOfWeek.Sunday, new TimeOnly(8, 0), new TimeOnly(12, 0)));
        await context.SaveChangesAsync();

        // Assert
        List<AuditLogEntry> calendarEntries = await context.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(SlaCalendar) && a.EntityId == calendar.Id.ToString())
            .OrderBy(a => a.Timestamp)
            .ToListAsync();
        bool holidayAdded = await context.AuditLogs.AnyAsync(a =>
            a.EntityName == nameof(SlaCalendarHoliday) && a.EntityId == holiday.Id.ToString() && a.Action == "Added");

        calendarEntries.Select(a => a.Action).ShouldBe(["Added", "Modified", "Modified"]);
        calendarEntries[1].AffectedColumns.ShouldBe(nameof(SlaCalendar.Name));
        calendarEntries[2].AffectedColumns.ShouldBe(nameof(SlaCalendar.WorkSchedule));
        holidayAdded.ShouldBeTrue();
    }

    // Sábado, das 08:00 às 12:00 (AC-18).
    private static List<SlaWorkPeriod> SaturdayMorning() =>
        [new SlaWorkPeriod(DayOfWeek.Saturday, new TimeOnly(8, 0), new TimeOnly(12, 0))];

    private static SlaCalendar NewCalendar(List<SlaWorkPeriod> workSchedule) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Plantão de sábado",
        TimeZoneId = "America/Sao_Paulo",
        Is24x7 = false,
        WorkSchedule = workSchedule
    };

    private async Task<SlaCalendar> AddCalendarAsync(List<SlaWorkPeriod> workSchedule)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        SlaCalendar calendar = NewCalendar(workSchedule);
        context.SlaCalendars.Add(calendar);
        await context.SaveChangesAsync();

        return calendar;
    }

    private async Task AddHolidayAsync(Guid calendarId, DateOnly date)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.SlaCalendarHolidays.Add(new SlaCalendarHoliday
        {
            Id = Guid.NewGuid(),
            SlaCalendarId = calendarId,
            Date = date,
            Description = "Feriado de teste"
        });
        await context.SaveChangesAsync();
    }

    private async Task<DateTime> AddWithProviderAsync(Guid calendarId, DateTime startUtc, TimeSpan duration)
    {
        using IServiceScope scope = Services.CreateScope();
        Result<BusinessCalendar> result = await scope.ServiceProvider.GetRequiredService<ISlaCalendarProvider>()
            .GetAsync(calendarId);

        return result.Value.AddBusinessTime(startUtc, duration);
    }

    private static DateTime Parse(string local) =>
        DateTime.ParseExact(local, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static DateTime At(string local) => TimeZoneInfo.ConvertTimeToUtc(Parse(local), SaoPaulo);

    private static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, SaoPaulo);
}
