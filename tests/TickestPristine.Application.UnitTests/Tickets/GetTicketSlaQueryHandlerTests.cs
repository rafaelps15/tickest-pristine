using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Application.Tickets.GetSla;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class GetTicketSlaQueryHandlerTests : BaseHandlerTest
{
    private static readonly Guid RequesterId = Guid.NewGuid();

    // Segunda-feira, 26/10/2026, 08:10 em São Paulo; consultado às 11:15.
    private static readonly DateTime OpenedAtUtc = new(2026, 10, 26, 11, 10, 0, DateTimeKind.Utc);
    private static readonly DateTime NowUtc = new(2026, 10, 26, 14, 15, 0, DateTimeKind.Utc);

    private static GetTicketSlaQueryHandler CreateHandler(TestDbContext context, Guid callerId, bool canManage = false)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(canManage);

        return new GetTicketSlaQueryHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), CreateClock(NowUtc));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var query = new GetTicketSlaQuery(Guid.NewGuid());

        // Act
        Result<TicketSlaResponse> result = await CreateHandler(context, RequesterId, canManage: true).Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotFound(query.TicketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotParticipantAndLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketWithSlaAsync(context, RequesterId, OpenedAtUtc);

        // Act
        Result<TicketSlaResponse> result = await CreateHandler(context, Guid.NewGuid()).Handle(
            new GetTicketSlaQuery(ticket.Id),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnPlanAndObjectivesWithAllFields_WhenTicketIsNew()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketWithSlaAsync(context, RequesterId, OpenedAtUtc);

        // Act
        Result<TicketSlaResponse> result = await CreateHandler(context, RequesterId).Handle(
            new GetTicketSlaQuery(ticket.Id),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        TicketSlaResponse sla = result.Value;
        sla.ServerNowUtc.ShouldBe(NowUtc);
        sla.Plan.PolicyId.ShouldBeNull();
        sla.Plan.PolicyName.ShouldBe("Padrão");
        sla.Plan.PolicyVersion.ShouldBe(1);
        sla.Plan.CalendarId.ShouldBe(SlaCalendarIds.Default);
        sla.Plan.CalendarName.ShouldBe("Expediente");
        sla.Plan.IsLegacy.ShouldBeFalse();

        sla.Objectives.Select(o => o.Metric).ShouldBe([SlaMetric.FirstResponse, SlaMetric.Resolution]);

        TicketSlaObjectiveResponse firstResponse = sla.Objectives[0];
        firstResponse.Cycle.ShouldBe(1);
        firstResponse.State.ShouldBe(SlaObjectiveState.Breached);
        firstResponse.TargetMinutes.ShouldBe(30);
        firstResponse.DueAtUtc.ShouldBe(OpenedAtUtc.AddMinutes(30));
        firstResponse.RemainingMinutes.ShouldBe(0);
        firstResponse.IsCurrentCycle.ShouldBeTrue();

        TicketSlaObjectiveResponse resolution = sla.Objectives[1];
        resolution.Cycle.ShouldBe(1);
        resolution.State.ShouldBe(SlaObjectiveState.AtRisk);
        resolution.TargetMinutes.ShouldBe(240);
        resolution.StartedAtUtc.ShouldBe(OpenedAtUtc);
        resolution.WarningAtUtc.ShouldBe(OpenedAtUtc.AddMinutes(180));
        resolution.DueAtUtc.ShouldBe(OpenedAtUtc.AddMinutes(240));
        resolution.CompletedAtUtc.ShouldBeNull();
        resolution.StoppedAtUtc.ShouldBeNull();
        resolution.ConsumedMinutes.ShouldBe(185);
        resolution.RemainingMinutes.ShouldBe(55);
        resolution.IsCurrentCycle.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnFirstResponseNotApplicable_WhenTicketIsLegacy()
    {
        // Arrange: chamado anterior ao plano, só com o cronômetro de resolução em horas corridas.
        await using TestDbContext context = CreateDbContext();
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Legacy ticket",
            Description = "Opened before the SLA plan",
            Priority = TicketPriority.Urgent,
            Status = TicketStatus.Open,
            CreatedByUserId = RequesterId,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = OpenedAtUtc,
            DueAtUtc = OpenedAtUtc.AddHours(4),
            SlaWarningAtUtc = OpenedAtUtc.AddHours(3),
            SlaPlan = TicketSla.LegacyPlan(TicketPriority.Urgent)
        };
        context.Tickets.Add(ticket);
        context.TicketSlaTimers.Add(new TicketSlaTimer
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            Metric = SlaMetric.Resolution,
            Cycle = 1,
            TargetMinutes = 240,
            CalendarId = SlaCalendarIds.Continuous,
            StartedAtUtc = ticket.CreatedAtUtc,
            WarningOnUtc = ticket.SlaWarningAtUtc,
            DueOnUtc = ticket.DueAtUtc
        });
        await context.SaveChangesAsync();

        // Act
        Result<TicketSlaResponse> result = await CreateHandler(context, RequesterId).Handle(
            new GetTicketSlaQuery(ticket.Id),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plan.IsLegacy.ShouldBeTrue();
        result.Value.Plan.CalendarName.ShouldBe("24 horas");

        TicketSlaObjectiveResponse firstResponse = result.Value.Objectives.Single(o => o.Metric == SlaMetric.FirstResponse);
        firstResponse.State.ShouldBe(SlaObjectiveState.NotApplicable);
        firstResponse.IsCurrentCycle.ShouldBeTrue();
        firstResponse.TargetMinutes.ShouldBeNull();
        firstResponse.StartedAtUtc.ShouldBeNull();
        firstResponse.WarningAtUtc.ShouldBeNull();
        firstResponse.DueAtUtc.ShouldBeNull();
        firstResponse.CompletedAtUtc.ShouldBeNull();
        firstResponse.StoppedAtUtc.ShouldBeNull();

        TicketSlaObjectiveResponse resolution = result.Value.Objectives.Single(o => o.Metric == SlaMetric.Resolution);
        resolution.State.ShouldBe(SlaObjectiveState.AtRisk);
        resolution.ConsumedMinutes.ShouldBe(185);
    }

    [Fact]
    public async Task Handle_Should_ReturnRecordedPlanAndDeadlines_WhenPolicyChangedAfterOpening()
    {
        // Arrange: chamado aberto quando a política tinha outras metas (versão anterior, 10 e 60 minutos).
        await using TestDbContext context = CreateDbContext();
        var olderPlan = new TicketSlaPlan(null, "Padrão", 0, SlaCalendarIds.Default, 10, 60, IsLegacy: false);
        var ticketId = Guid.NewGuid();
        List<TicketSlaTimer> timers = TicketSlaClock.Start(ticketId, olderPlan, DefaultSlaCalendar.Create(), OpenedAtUtc);
        TicketSlaTimer recordedResolution = timers.Single(t => t.Metric == SlaMetric.Resolution);

        context.Tickets.Add(new Ticket
        {
            Id = ticketId,
            Title = "Printer is broken",
            Description = "The printer on the 3rd floor is not working",
            Priority = TicketPriority.Urgent,
            Status = TicketStatus.Open,
            CreatedByUserId = RequesterId,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = OpenedAtUtc,
            DueAtUtc = recordedResolution.DueOnUtc,
            SlaWarningAtUtc = recordedResolution.WarningOnUtc,
            SlaPlan = olderPlan
        });
        context.TicketSlaTimers.AddRange(timers);
        await context.SaveChangesAsync();

        // Act
        Result<TicketSlaResponse> result = await CreateHandler(context, RequesterId).Handle(
            new GetTicketSlaQuery(ticketId),
            CancellationToken.None);

        // Assert: nem o plano nem os prazos seguem a política atual (30 e 240 minutos para Urgente).
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plan.PolicyVersion.ShouldBe(0);

        TicketSlaObjectiveResponse resolution = result.Value.Objectives.Single(o => o.Metric == SlaMetric.Resolution);
        resolution.TargetMinutes.ShouldBe(60);
        resolution.DueAtUtc.ShouldBe(recordedResolution.DueOnUtc);
        result.Value.Objectives.Single(o => o.Metric == SlaMetric.FirstResponse).TargetMinutes.ShouldBe(10);
    }

    [Fact]
    public async Task Handle_Should_ReturnCalendarNotFound_WhenPlanCalendarDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketWithSlaAsync(context, RequesterId, OpenedAtUtc);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(RequesterId);

        var handler = new GetTicketSlaQueryHandler(
            context,
            userContext,
            Substitute.For<IPermissionProvider>(),
            CreateMissingSlaCalendarProvider(),
            CreateClock(NowUtc));

        // Act
        Result<TicketSlaResponse> result = await handler.Handle(new GetTicketSlaQuery(ticket.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SlaCalendarErrors.NotFound(ticket.SlaPlan.CalendarId));
    }
}
