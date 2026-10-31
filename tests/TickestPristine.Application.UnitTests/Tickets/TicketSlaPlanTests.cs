using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class TicketSlaPlanTests
{
    private static readonly DateTime CreatedAtUtc = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

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
}
