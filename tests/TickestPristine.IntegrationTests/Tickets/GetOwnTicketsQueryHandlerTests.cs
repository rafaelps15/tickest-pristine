using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Tickets;
using TickestPristine.Application.Tickets.GetOwn;
using TickestPristine.IntegrationTests.Abstractions;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using NSubstitute;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.Tickets;

public sealed class GetOwnTicketsQueryHandlerTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid AgentId = Guid.NewGuid();
    private static readonly DateTime NowUtc = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    // Abertura dos chamados com cronômetros: no calendário 24 horas, a resolução de 240 minutos de cada ciclo vence 4 h
    // depois do início dele, e o alerta, 3 h depois; o ciclo 1 vence às 12h, com o alerta às 11h.
    private static readonly DateTime OpenedAtUtc = new(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
    private static readonly TicketSlaPlan Plan = new(null, "Padrão", 1, SlaCalendarIds.Continuous, 30, 240, IsLegacy: false);
    private static readonly BusinessCalendar Calendar = ContinuousSlaCalendar.Create();

    private readonly TicketSlaCycleService _slaCycle = new();

    [Fact]
    public async Task Handle_Should_ReturnOnlyCallerTickets_WhenTicketsHaveDifferentStatuses()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1));
        await SeedTicketAsync(context, OwnerId, TicketStatus.InProgress, NowUtc.AddHours(-2));
        await SeedTicketAsync(context, OwnerId, TicketStatus.Resolved, NowUtc.AddHours(-3));
        await SeedTicketAsync(context, Guid.NewGuid(), TicketStatus.Open, NowUtc.AddHours(-1));

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(3);
        result.Value.Items.ShouldAllBe(t => t.OpenedByUserId == OwnerId);
    }

    [Fact]
    public async Task Handle_Should_ReturnNewestFirst_WhenCallerHasSeveralTickets()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        Ticket oldest = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-20));
        Ticket newest = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1));
        Ticket middle = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-10));

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.Value.Items.Select(t => t.Id).ShouldBe([newest.Id, middle.Id, oldest.Id]);
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyPage_WhenCallerHasNoTickets()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1));

        GetOwnTicketsQueryHandler handler = CreateHandler(Guid.NewGuid());

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_ReturnAssigneeSectorDueAndSla_WhenTicketIsAssigned()
    {
        // Arrange: um chamado no Helpdesk atribuído a Ana, com o prazo no futuro, e outro sem responsável
        await using ApplicationDbContext context = CreateDbContext();
        User ana = CreateUser();
        ana.FirstName = "Ana";
        ana.LastName = "Souza";
        var helpdesk = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), "Helpdesk", description: null);
        context.Users.Add(ana);
        context.Sectors.Add(helpdesk);
        await context.SaveChangesAsync();

        Ticket assigned = await SeedTicketAsync(
            context,
            OwnerId,
            TicketStatus.InProgress,
            NowUtc.AddHours(-2),
            sector: helpdesk,
            assignedToUserId: ana.Id);
        Ticket unassigned = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1), sector: helpdesk);

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        OwnTicketResponse withAssignee = result.Value.Items.Single(t => t.Id == assigned.Id);
        withAssignee.AssignedToUserId.ShouldBe(ana.Id);
        withAssignee.AssignedToName.ShouldBe("Ana Souza");
        withAssignee.SectorId.ShouldBe(helpdesk.Id);
        withAssignee.SectorName.ShouldBe("Helpdesk");
        withAssignee.DueAtUtc.ShouldBe(assigned.DueAtUtc);
        withAssignee.SlaStatus.ShouldBe(TicketSlaStatus.OnTime);
        withAssignee.LastActivityAtUtc.ShouldBe(assigned.LastActivityAtUtc);

        result.Value.Items.Single(t => t.Id == unassigned.Id).AssignedToName.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_FilterByStatusGroup_WhenGroupIsGiven()
    {
        // Arrange: um chamado de cada status
        await using ApplicationDbContext context = CreateDbContext();
        foreach (TicketStatus status in Enum.GetValues<TicketStatus>())
        {
            await SeedTicketAsync(context, OwnerId, status, NowUtc.AddHours(-1));
        }

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> active = await handler.Handle(
            CreateQuery(statusGroup: OwnTicketStatusGroup.Active),
            CancellationToken.None);
        Result<PagedResponse<OwnTicketResponse>> finished = await handler.Handle(
            CreateQuery(statusGroup: OwnTicketStatusGroup.Finished),
            CancellationToken.None);
        Result<PagedResponse<OwnTicketResponse>> all = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        active.Value.Items.Select(t => t.Status).Order().ShouldBe([TicketStatus.Open, TicketStatus.InProgress]);
        finished.Value.Items.Select(t => t.Status).Order()
            .ShouldBe([TicketStatus.Resolved, TicketStatus.Closed, TicketStatus.Canceled]);
        all.Value.TotalCount.ShouldBe(5);
    }

    [Fact]
    public async Task Handle_Should_SearchTitleIgnoringSurroundingSpaces_WhenSearchIsGiven()
    {
        // Arrange (maiúsculas e minúsculas são conferidas no teste de integração, com o citext do PostgreSQL)
        await using ApplicationDbContext context = CreateDbContext();
        Ticket printer = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1), title: "Impressora não imprime");
        Ticket email = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-2), title: "Acesso ao e-mail");

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> byPrinter = await handler.Handle(
            CreateQuery(search: "Impress"),
            CancellationToken.None);
        Result<PagedResponse<OwnTicketResponse>> byEmail = await handler.Handle(
            CreateQuery(search: "  e-mail  "),
            CancellationToken.None);

        // Assert
        byPrinter.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(printer.Id);
        byEmail.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(email.Id);
    }

    [Fact]
    public async Task Handle_Should_IgnoreSearch_WhenSearchIsBlank()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1), title: "Impressora não imprime");
        await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-2), title: "Acesso ao e-mail");

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(
            CreateQuery(search: "   "),
            CancellationToken.None);

        // Assert
        result.Value.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_SearchTitleOrDescription_WhenSearchIsGiven()
    {
        // Arrange (maiúsculas e minúsculas são conferidas no teste de integração, com o citext do PostgreSQL)
        await using ApplicationDbContext context = CreateDbContext();
        (Ticket printer, Ticket email, Ticket monitor) = await SeedSearchScenarioAsync(context);

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> byDescription = await handler.Handle(
            CreateQuery(search: "SENHA"),
            CancellationToken.None);
        Result<PagedResponse<OwnTicketResponse>> byTitle = await handler.Handle(
            CreateQuery(search: "Impress"),
            CancellationToken.None);
        Result<PagedResponse<OwnTicketResponse>> byTitleOrDescription = await handler.Handle(
            CreateQuery(search: "e"),
            CancellationToken.None);

        // Assert
        byDescription.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(email.Id);
        byTitle.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(printer.Id);
        byTitleOrDescription.Value.Items.Select(t => t.Id).ShouldBe([printer.Id, email.Id, monitor.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Handle_Should_CombineDescriptionSearchWithStatusGroup_WhenBothAreGiven()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        (Ticket printer, Ticket email, Ticket monitor) = await SeedSearchScenarioAsync(context);

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> activeWithE = await handler.Handle(
            CreateQuery(OwnTicketStatusGroup.Active, "e"),
            CancellationToken.None);
        Result<PagedResponse<OwnTicketResponse>> finishedWithPassword = await handler.Handle(
            CreateQuery(OwnTicketStatusGroup.Finished, "SENHA"),
            CancellationToken.None);
        Result<PagedResponse<OwnTicketResponse>> activeWithPassword = await handler.Handle(
            CreateQuery(OwnTicketStatusGroup.Active, "SENHA"),
            CancellationToken.None);

        // Assert
        activeWithE.Value.Items.Select(t => t.Id).ShouldBe([printer.Id, monitor.Id]);
        activeWithE.Value.TotalCount.ShouldBe(2);
        finishedWithPassword.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(email.Id);
        activeWithPassword.Value.Items.ShouldBeEmpty();
        activeWithPassword.Value.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_NotReturnOtherUsersTickets_WhenDescriptionMatchesSearch()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        (_, Ticket email, _) = await SeedSearchScenarioAsync(context);

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(
            CreateQuery(search: "SENHA"),
            CancellationToken.None);

        // Assert
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(email.Id);
    }

    [Fact]
    public async Task Handle_Should_OrderByLastActivityDescending_WhenActivityDiffersFromCreation()
    {
        // Arrange: A, B e C abertos nessa ordem; A teve atividade depois da abertura de C
        await using ApplicationDbContext context = CreateDbContext();
        Ticket a = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-3), lastActivityAtUtc: NowUtc.AddMinutes(-5));
        Ticket b = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-2));
        Ticket c = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1));

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.Value.Items.Select(t => t.Id).ShouldBe([a.Id, c.Id, b.Id]);
    }

    [Fact]
    public async Task Handle_Should_ReturnCanCancelOnlyWithinOwnerWindow_WhenCallerIsOwner()
    {
        // Arrange: chamado aberto às 9h00; a janela do dono vai até 9h15
        await using ApplicationDbContext context = CreateDbContext();
        var openedAtUtc = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);
        Ticket ticket = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, openedAtUtc);

        // Act
        Result<PagedResponse<OwnTicketResponse>> at0910 = await CreateHandler(OwnerId, openedAtUtc.AddMinutes(10))
            .Handle(CreateQuery(), CancellationToken.None);
        Result<PagedResponse<OwnTicketResponse>> at0920 = await CreateHandler(OwnerId, openedAtUtc.AddMinutes(20))
            .Handle(CreateQuery(), CancellationToken.None);

        // Assert
        OwnTicketResponse early = at0910.Value.Items.ShouldHaveSingleItem();
        early.CanCancel.ShouldBeTrue();
        early.OwnerCancelDeadlineUtc.ShouldBe(openedAtUtc.AddMinutes(15));

        OwnTicketResponse late = at0920.Value.Items.ShouldHaveSingleItem();
        late.CanCancel.ShouldBeFalse();
        late.OwnerCancelDeadlineUtc.ShouldBe(openedAtUtc.AddMinutes(15));
        late.Id.ShouldBe(ticket.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnCanCancelAfterWindow_WhenCallerHasCancelPermission()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        var openedAtUtc = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);
        await SeedTicketAsync(context, OwnerId, TicketStatus.Open, openedAtUtc);

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId, openedAtUtc.AddHours(2), canCancelAny: true);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.Value.Items.ShouldHaveSingleItem().CanCancel.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnCannotCancel_WhenTicketIsInactive()
    {
        // Arrange: chamado cancelado dentro da janela, consultado por quem tem tickets:cancel
        await using ApplicationDbContext context = CreateDbContext();
        var openedAtUtc = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);
        await SeedTicketAsync(context, OwnerId, TicketStatus.Canceled, openedAtUtc);

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId, openedAtUtc.AddMinutes(5), canCancelAny: true);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.Value.Items.ShouldHaveSingleItem().CanCancel.ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnSameCurrentCycle_WhenNeverReopened()
    {
        // Arrange: chamado aberto às 8h, consultado às 9h
        await using ApplicationDbContext context = CreateDbContext();
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenTicketWithSlaTimers();
        await SaveAsync(context, ticket, timers);

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId, OpenedAtUtc.AddHours(1));

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        OwnTicketResponse item = result.Value.Items.ShouldHaveSingleItem();
        item.CurrentDueAtUtc.ShouldBe(OpenedAtUtc.AddHours(4));
        item.CurrentDueAtUtc.ShouldBe(item.DueAtUtc);
        item.CurrentSlaStatus.ShouldBe(TicketSlaStatus.OnTime);
        item.CurrentSlaStatus.ShouldBe(item.SlaStatus);
    }

    [Fact]
    public async Task Handle_Should_ReturnCurrentResolutionCycle_WhenReopened()
    {
        // Arrange: resolvido às 9h, dentro do prazo das 12h, e reaberto às 10h; o ciclo 2 vence às 14h
        await using ApplicationDbContext context = CreateDbContext();
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenTicketWithSlaTimers();
        TicketSlaTimer currentCycle = ResolveAndReopen(ticket, timers, OpenedAtUtc.AddHours(1));
        await SaveAsync(context, ticket, timers);

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId, OpenedAtUtc.AddHours(2).AddMinutes(30));

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        OwnTicketResponse item = result.Value.Items.ShouldHaveSingleItem();
        item.SlaStatus.ShouldBe(TicketSlaStatus.Met);
        item.DueAtUtc.ShouldBe(OpenedAtUtc.AddHours(4));
        currentCycle.Cycle.ShouldBe(2);
        item.CurrentDueAtUtc.ShouldBe(currentCycle.DueOnUtc);
        item.CurrentDueAtUtc.ShouldBe(OpenedAtUtc.AddHours(6));
        item.CurrentSlaStatus.ShouldBe(TicketSlaStatus.OnTime);
    }

    [Theory]
    [InlineData(3, TicketSlaStatus.AtRisk, SlaObjectiveState.AtRisk)]
    [InlineData(4, TicketSlaStatus.Breached, SlaObjectiveState.Breached)]
    public async Task Handle_Should_FollowStateOfCurrentCycle_WhenTimePassesAfterReopen(
        int hoursAfterReopen,
        TicketSlaStatus expectedStatus,
        SlaObjectiveState expectedCycleState)
    {
        // Arrange: reaberto às 10h; o alerta do ciclo 2 é às 13h e o prazo, às 14h
        await using ApplicationDbContext context = CreateDbContext();
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenTicketWithSlaTimers();
        TicketSlaTimer currentCycle = ResolveAndReopen(ticket, timers, OpenedAtUtc.AddHours(1));
        await SaveAsync(context, ticket, timers);

        DateTime nowUtc = OpenedAtUtc.AddHours(2 + hoursAfterReopen);
        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId, nowUtc);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert: a mesma situação que o método do cronômetro dá ao ciclo vigente, e a do chamado continua congelada
        OwnTicketResponse item = result.Value.Items.ShouldHaveSingleItem();
        item.CurrentSlaStatus.ShouldBe(expectedStatus);
        currentCycle.GetState(nowUtc).ShouldBe(expectedCycleState);
        item.SlaStatus.ShouldBe(TicketSlaStatus.Met);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotApplicable_WhenResolutionTimerIsStopped()
    {
        // Arrange: resolvido às 9h, dentro do prazo, reaberto às 10h e cancelado às 10h30, com o ciclo 2 parado
        await using ApplicationDbContext context = CreateDbContext();
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenTicketWithSlaTimers();
        ResolveAndReopen(ticket, timers, OpenedAtUtc.AddHours(1));
        _slaCycle.Cancel(ticket, timers, canCancelAny: true, isOwnerWhoCanEdit: false, AgentId, OpenedAtUtc.AddHours(2).AddMinutes(30));
        await SaveAsync(context, ticket, timers);

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId, OpenedAtUtc.AddHours(3));

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        OwnTicketResponse item = result.Value.Items.ShouldHaveSingleItem();
        item.CurrentSlaStatus.ShouldBe(TicketSlaStatus.NotApplicable);
        item.SlaStatus.ShouldBe(TicketSlaStatus.Met);
    }

    [Fact]
    public async Task Handle_Should_ReturnMissed_WhenResolvedAfterDue()
    {
        // Arrange: resolvido às 9h, dentro do prazo, reaberto às 10h e resolvido de novo às 15h, depois do prazo das 14h
        // do ciclo 2
        await using ApplicationDbContext context = CreateDbContext();
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenTicketWithSlaTimers();
        ResolveAndReopen(ticket, timers, OpenedAtUtc.AddHours(1));
        DateTime resolvedAgainAtUtc = OpenedAtUtc.AddHours(7);
        ticket.StartProgress(lastMessageIsFromAgent: false, AgentId, resolvedAgainAtUtc);
        _slaCycle.Resolve(ticket, timers, AgentId, resolvedAgainAtUtc);
        await SaveAsync(context, ticket, timers);

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId, OpenedAtUtc.AddHours(8));

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        OwnTicketResponse item = result.Value.Items.ShouldHaveSingleItem();
        item.CurrentSlaStatus.ShouldBe(TicketSlaStatus.Missed);
        item.SlaStatus.ShouldBe(TicketSlaStatus.Met);
    }

    [Fact]
    public async Task Handle_Should_ReturnMet_WhenCurrentCycleIsResolvedExactlyAtItsDue()
    {
        // Arrange: a primeira resolução, às 13h, passou do prazo das 12h; reaberto às 14h, o ciclo 2 vence às 18h e é
        // resolvido exatamente às 18h; consultado às 19h, depois desse prazo
        await using ApplicationDbContext context = CreateDbContext();
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenTicketWithSlaTimers();
        TicketSlaTimer currentCycle = ResolveAndReopen(ticket, timers, OpenedAtUtc.AddHours(5));
        ticket.StartProgress(lastMessageIsFromAgent: false, AgentId, currentCycle.DueOnUtc);
        _slaCycle.Resolve(ticket, timers, AgentId, currentCycle.DueOnUtc);
        await SaveAsync(context, ticket, timers);

        DateTime nowUtc = OpenedAtUtc.AddHours(11);
        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId, nowUtc);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert: o ciclo vigente cumprido no limite do prazo, como o método do cronômetro diz, e o chamado violado
        OwnTicketResponse item = result.Value.Items.ShouldHaveSingleItem();
        currentCycle.DueOnUtc.ShouldBe(OpenedAtUtc.AddHours(10));
        item.CurrentSlaStatus.ShouldBe(TicketSlaStatus.Met);
        currentCycle.GetState(nowUtc).ShouldBe(SlaObjectiveState.Met);
        item.SlaStatus.ShouldBe(TicketSlaStatus.Missed);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotApplicable_WhenStoppedCycleIsPastItsDue()
    {
        // Arrange: reaberto às 10h, com o ciclo 2 vencendo às 14h, e cancelado às 10h30; consultado às 15h, depois do
        // prazo do ciclo parado
        await using ApplicationDbContext context = CreateDbContext();
        (Ticket ticket, List<TicketSlaTimer> timers) = OpenTicketWithSlaTimers();
        TicketSlaTimer currentCycle = ResolveAndReopen(ticket, timers, OpenedAtUtc.AddHours(1));
        _slaCycle.Cancel(ticket, timers, canCancelAny: true, isOwnerWhoCanEdit: false, AgentId, OpenedAtUtc.AddHours(2).AddMinutes(30));
        await SaveAsync(context, ticket, timers);

        DateTime nowUtc = OpenedAtUtc.AddHours(7);
        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId, nowUtc);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert: parado vale antes de violado, como no método do cronômetro
        OwnTicketResponse item = result.Value.Items.ShouldHaveSingleItem();
        item.CurrentSlaStatus.ShouldBe(TicketSlaStatus.NotApplicable);
        currentCycle.GetState(nowUtc).ShouldBe(SlaObjectiveState.Stopped);
        item.SlaStatus.ShouldBe(TicketSlaStatus.Met);
    }

    [Fact]
    public async Task Handle_Should_FallBackToTicketSla_WhenNoResolutionTimer()
    {
        // Arrange: chamado do plano legado, sem cronômetro, aberto há 20 horas: passou do alerta (18 h), não do prazo (24 h)
        await using ApplicationDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketAsync(context, OwnerId, TicketStatus.InProgress, NowUtc.AddHours(-20));

        GetOwnTicketsQueryHandler handler = CreateHandler(OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        OwnTicketResponse item = result.Value.Items.ShouldHaveSingleItem();
        item.SlaStatus.ShouldBe(TicketSlaStatus.AtRisk);
        item.CurrentSlaStatus.ShouldBe(item.SlaStatus);
        item.CurrentDueAtUtc.ShouldBe(ticket.DueAtUtc);
    }

    // Chamado de quem consulta, aberto às 8h pelo Ticket.Create com o plano de 240 minutos e com os cronômetros do ciclo
    // 1 criados pelo TicketSlaCycleService, como na abertura pela API.
    private (Ticket Ticket, List<TicketSlaTimer> Timers) OpenTicketWithSlaTimers()
    {
        var requester = new User { Id = OwnerId };

        var ticket = Ticket.Create(
            "Impressora parada",
            "A impressora do terceiro andar não liga",
            TicketPriority.Urgent,
            QueryTicketTestData.CreateSector(),
            requester,
            assignee: null,
            Plan,
            Calendar,
            requester.Id,
            OpenedAtUtc);

        return (ticket, [.. _slaCycle.Start(ticket, Calendar)]);
    }

    // Resolve o chamado no instante informado, passando por Em andamento, e o reabre uma hora depois; devolve o
    // cronômetro de resolução do ciclo novo, já acrescentado à lista.
    private TicketSlaTimer ResolveAndReopen(Ticket ticket, List<TicketSlaTimer> timers, DateTime resolvedAtUtc)
    {
        ticket.StartProgress(lastMessageIsFromAgent: false, AgentId, resolvedAtUtc);
        _slaCycle.Resolve(ticket, timers, AgentId, resolvedAtUtc);

        TicketSlaTimer newCycle = _slaCycle
            .Reopen(ticket, timers, Calendar, lastMessageIsFromAgent: false, AgentId, resolvedAtUtc.AddHours(1))
            .Value.ResolutionCycle;
        timers.Add(newCycle);

        return newCycle;
    }

    private static async Task SaveAsync(ApplicationDbContext context, Ticket ticket, List<TicketSlaTimer> timers)
    {
        context.Tickets.Add(ticket);
        context.TicketSlaTimers.AddRange(timers);
        await context.SaveChangesAsync();
    }

    // Handler para quem consulta, por padrão um Colaborador: pode editar os próprios chamados (tickets:update-own) e
    // não tem tickets:cancel.
    private GetOwnTicketsQueryHandler CreateHandler(
        Guid callerId,
        DateTime? nowUtc = null,
        bool canCancelAny = false)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(true);
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.Cancel, Arg.Any<CancellationToken>())
            .Returns(canCancelAny);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(nowUtc ?? NowUtc);

        return new GetOwnTicketsQueryHandler(DbConnectionFactory, userContext, permissionProvider, dateTimeProvider);
    }

    private static GetOwnTicketsQuery CreateQuery(OwnTicketStatusGroup? statusGroup = null, string? search = null) =>
        new(statusGroup, search, new PageRequest(1, 20));

    // Chamados do cenário: "Impressora não imprime" (Aberto), "Acesso ao e-mail" (Resolvido) e "Monitor piscando" (Em
    // Andamento, com "e" só na descrição), da última atividade mais recente para a mais antiga, e um chamado de outro
    // usuário com "SENHA" na descrição.
    private static async Task<(Ticket Printer, Ticket Email, Ticket Monitor)> SeedSearchScenarioAsync(ApplicationDbContext context)
    {
        Ticket printer = await SeedTicketAsync(
            context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1), title: "Impressora não imprime", description: "Sai papel em branco");
        Ticket email = await SeedTicketAsync(
            context, OwnerId, TicketStatus.Resolved, NowUtc.AddHours(-2), title: "Acesso ao e-mail", description: "Outlook pede a SENHA toda hora");
        Ticket monitor = await SeedTicketAsync(
            context, OwnerId, TicketStatus.InProgress, NowUtc.AddHours(-3), title: "Monitor piscando", description: "Desde ontem");
        await SeedTicketAsync(
            context, Guid.NewGuid(), TicketStatus.Open, NowUtc.AddHours(-4), title: "Bloqueio", description: "SENHA bloqueada");

        return (printer, email, monitor);
    }

    private static async Task<Ticket> SeedTicketAsync(
        ApplicationDbContext context,
        Guid openedByUserId,
        TicketStatus status,
        DateTime createdAtUtc,
        string title = "Printer is broken",
        Sector? sector = null,
        Guid? assignedToUserId = null,
        DateTime? lastActivityAtUtc = null,
        string description = "Original description")
    {
        Ticket ticket = QueryTicketTestData.Create(
            requesterId: openedByUserId,
            sector: sector,
            status: status,
            createdAtUtc: createdAtUtc,
            assignedToUserId: assignedToUserId,
            title: title,
            description: description,
            dueAtUtc: createdAtUtc.AddHours(24),
            slaWarningAtUtc: createdAtUtc.AddHours(18));

        context.Tickets.Add(ticket);

        // O Create já começa a última atividade na data de abertura; outra data só entra pelo EF.
        if (lastActivityAtUtc is { } lastActivity)
        {
            context.Entry(ticket).Property(t => t.LastActivityAtUtc).CurrentValue = lastActivity;
        }

        await context.SaveChangesAsync();

        return ticket;
    }
}
