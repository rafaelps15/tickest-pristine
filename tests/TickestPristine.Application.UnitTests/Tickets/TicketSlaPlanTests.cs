using System.Globalization;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class TicketSlaPlanTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    // Segunda-feira, 26/10/2026, 08:00 em São Paulo: início da faixa do "Expediente".
    private const string StartedAt = "2026-10-26 08:00:00";

    [Theory]
    [InlineData(TicketPriority.Urgent, 30, 240)]
    [InlineData(TicketPriority.High, 120, 600)]
    [InlineData(TicketPriority.Medium, 240, 1200)]
    [InlineData(TicketPriority.Low, 600, 3000)]
    public void Default_Should_ReturnDefaultPolicyTargetsInGivenCalendar(
        TicketPriority priority,
        int firstResponseMinutes,
        int resolutionMinutes)
    {
        // Act
        var plan = TicketSlaPlan.Default(priority);

        // Assert
        plan.PolicyId.ShouldBeNull();
        plan.PolicyName.ShouldBe("Padrão");
        plan.PolicyVersion.ShouldBe(1);
        plan.CalendarId.ShouldBe(SlaCalendarIds.Default);
        plan.FirstResponseMinutes.ShouldBe(firstResponseMinutes);
        plan.ResolutionMinutes.ShouldBe(resolutionMinutes);
        plan.IsLegacy.ShouldBeFalse();
    }

    [Theory]
    [InlineData(TicketPriority.Urgent, 240)]
    [InlineData(TicketPriority.High, 480)]
    [InlineData(TicketPriority.Medium, 1440)]
    [InlineData(TicketPriority.Low, 4320)]
    public void Legacy_Should_KeepCurrentHoursWithoutFirstResponseInGivenCalendar(TicketPriority priority, int resolutionMinutes)
    {
        // Act
        var plan = TicketSlaPlan.Legacy(priority);

        // Assert
        plan.PolicyName.ShouldBe("Legado (horas corridas)");
        plan.CalendarId.ShouldBe(SlaCalendarIds.Continuous);
        plan.FirstResponseMinutes.ShouldBeNull();
        plan.ResolutionMinutes.ShouldBe(resolutionMinutes);
        plan.IsLegacy.ShouldBeTrue();
    }

    [Theory]
    [InlineData(TicketPriority.Urgent, "2026-10-26 08:22:30", "2026-10-26 08:30:00", "2026-10-26 11:00:00", "2026-10-26 12:00:00")]
    [InlineData(TicketPriority.High, "2026-10-26 09:30:00", "2026-10-26 10:00:00", "2026-10-26 15:30:00", "2026-10-26 18:00:00")]
    [InlineData(TicketPriority.Medium, "2026-10-26 11:00:00", "2026-10-26 12:00:00", "2026-10-27 13:00:00", "2026-10-27 18:00:00")]
    [InlineData(TicketPriority.Low, "2026-10-26 15:30:00", "2026-10-26 18:00:00", "2026-10-29 15:30:00", "2026-10-30 18:00:00")]
    public void GetDeadlines_Should_ReturnWarningAtThreeQuartersAndDueAtTarget_WhenPlanIsDefault(
        TicketPriority priority,
        string firstResponseWarning,
        string firstResponseDue,
        string resolutionWarning,
        string resolutionDue)
    {
        // Arrange
        var plan = TicketSlaPlan.Default(priority);
        BusinessCalendar calendar = DefaultSlaCalendar.Create();

        // Act
        SlaDeadlines? firstResponse = plan.GetDeadlines(SlaMetric.FirstResponse, calendar, At(StartedAt));
        SlaDeadlines? resolution = plan.GetDeadlines(SlaMetric.Resolution, calendar, At(StartedAt));

        // Assert
        firstResponse.ShouldBe(new SlaDeadlines(At(firstResponseWarning), At(firstResponseDue)));
        resolution.ShouldBe(new SlaDeadlines(At(resolutionWarning), At(resolutionDue)));
    }

    [Fact]
    public void GetDeadlines_Should_CountContinuousTime_WhenCalendarIsContinuous()
    {
        // Arrange
        var plan = TicketSlaPlan.Default(TicketPriority.Urgent);

        // Act
        SlaDeadlines? firstResponse = plan.GetDeadlines(SlaMetric.FirstResponse, ContinuousSlaCalendar.Create(), CreatedAtUtc);

        // Assert
        firstResponse.ShouldBe(new SlaDeadlines(CreatedAtUtc.AddMinutes(22.5), CreatedAtUtc.AddMinutes(30)));
    }

    [Theory]
    [InlineData(TicketPriority.Urgent, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void GetDeadlines_Should_ReturnResolutionHoursAndNoFirstResponse_WhenPlanIsLegacy(TicketPriority priority, int hours)
    {
        // Arrange
        var plan = TicketSlaPlan.Legacy(priority);
        BusinessCalendar calendar = ContinuousSlaCalendar.Create();

        // Act
        SlaDeadlines? firstResponse = plan.GetDeadlines(SlaMetric.FirstResponse, calendar, CreatedAtUtc);
        SlaDeadlines? resolution = plan.GetDeadlines(SlaMetric.Resolution, calendar, CreatedAtUtc);

        // Assert
        firstResponse.ShouldBeNull();
        resolution.ShouldBe(new SlaDeadlines(CreatedAtUtc.AddHours(hours * 0.75), CreatedAtUtc.AddHours(hours)));
    }

    [Theory]
    [InlineData(TicketPriority.Urgent, "2026-10-26 11:00:00", "2026-10-26 12:00:00")]
    [InlineData(TicketPriority.Low, "2026-10-29 15:30:00", "2026-10-30 18:00:00")]
    public void Create_Should_SetResolutionDeadlinesInBusinessTime_WhenPlanIsDefault(
        TicketPriority priority,
        string warning,
        string due)
    {
        // Arrange
        var requester = new User { Id = Guid.NewGuid() };

        // Act
        var ticket = Ticket.Create(
            "Impressora parada",
            "A impressora do terceiro andar não liga",
            priority,
            TicketTestData.CreateSector(),
            requester,
            assignee: null,
            TicketSlaPlan.Default(priority),
            DefaultSlaCalendar.Create(),
            requester.Id,
            At(StartedAt));

        // Assert
        ticket.SlaWarningAtUtc.ShouldBe(At(warning));
        ticket.DueAtUtc.ShouldBe(At(due));
    }

    [Theory]
    [InlineData(TicketPriority.Urgent, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void Create_Should_SetDeadlinesInResolutionHoursOfPriority_WhenPlanIsLegacy(TicketPriority priority, int hours)
    {
        // Act
        Ticket ticket = CreateLegacyTicket(priority);

        // Assert
        ticket.DueAtUtc.ShouldBe(CreatedAtUtc.AddHours(hours));
        ticket.SlaWarningAtUtc.ShouldBe(CreatedAtUtc.AddHours(hours * 0.75));
    }

    private static Ticket CreateLegacyTicket(TicketPriority priority)
    {
        var requester = new User { Id = Guid.NewGuid() };

        return Ticket.Create(
            "Impressora parada",
            "A impressora do terceiro andar não liga",
            priority,
            TicketTestData.CreateSector(),
            requester,
            assignee: null,
            TicketSlaPlan.Legacy(priority),
            ContinuousSlaCalendar.Create(),
            requester.Id,
            CreatedAtUtc);
    }

    // Hora local de São Paulo convertida para UTC.
    private static DateTime At(string local) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.ParseExact(local, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            SaoPaulo);
}
