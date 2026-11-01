using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Tickets.GetAll;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class GetTicketsQueryHandlerTests : BaseHandlerTest
{
    private static readonly DateTime NowUtc = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_ReturnAllStatusesNewestFirst_WhenNoStatusIsInformed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        Ticket newest = await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-1));
        Ticket inProgress = await SeedTicketAsync(context, requester, sector, TicketStatus.InProgress, NowUtc.AddHours(-10));
        Ticket closed = await SeedTicketAsync(context, requester, sector, TicketStatus.Closed, NowUtc.AddHours(-20), NowUtc.AddHours(-19));

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(t => t.Id).ShouldBe([newest.Id, inProgress.Id, closed.Id]);
    }

    [Fact]
    public async Task Handle_Should_OrderByDueDate_WhenSortIsDueDate()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        Ticket later = await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-1));
        Ticket breached = await SeedTicketAsync(context, requester, sector, TicketStatus.InProgress, NowUtc.AddHours(-10));

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(
            CreateQuery(sort: TicketSortOrder.DueDate),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(t => t.Id).ShouldBe([breached.Id, later.Id]);
    }

    [Fact]
    public async Task Handle_Should_FilterByRequester_WhenRequesterIsInformed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        (User otherRequester, _) = await SeedRequesterAndSectorAsync(context);
        Ticket own = await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-1));
        await SeedTicketAsync(context, otherRequester, sector, TicketStatus.Open, NowUtc.AddHours(-1));

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(
            CreateQuery(requesterId: requester.Id),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(own.Id);
    }

    [Fact]
    public async Task Handle_Should_FilterByAssignee_WhenAssigneeIsInformed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        var assigneeId = Guid.NewGuid();
        Ticket assigned = await SeedTicketAsync(context, requester, sector, TicketStatus.InProgress, NowUtc.AddHours(-1), assignedToUserId: assigneeId);
        await SeedTicketAsync(context, requester, sector, TicketStatus.InProgress, NowUtc.AddHours(-1), assignedToUserId: Guid.NewGuid());
        await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-1));

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(
            CreateQuery(assignedToUserId: assigneeId),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(assigned.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnOnlyUnassignedTickets_WhenUnassignedOnlyIsTrue()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        Ticket unassigned = await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-1));
        await SeedTicketAsync(context, requester, sector, TicketStatus.InProgress, NowUtc.AddHours(-1), assignedToUserId: Guid.NewGuid());

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(
            CreateQuery(unassignedOnly: true),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(unassigned.Id);
    }

    [Fact]
    public async Task Handle_Should_IgnoreAssignee_WhenUnassignedOnlyIsTrue()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        var assigneeId = Guid.NewGuid();
        Ticket unassigned = await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-1));
        await SeedTicketAsync(context, requester, sector, TicketStatus.InProgress, NowUtc.AddHours(-1), assignedToUserId: assigneeId);

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(
            CreateQuery(assignedToUserId: assigneeId, unassignedOnly: true),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(unassigned.Id);
    }

    [Fact]
    public async Task Handle_Should_FillRequesterSectorAndSlaStatus_WhenTicketIsListed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        Ticket ticket = await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-3.5));

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        TicketSummaryResponse item = result.Value.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(ticket.Id);
        item.RequesterName.ShouldBe($"{requester.FirstName} {requester.LastName}");
        item.RequesterEmail.ShouldBe(requester.Email);
        item.SectorName.ShouldBe(sector.Name);
        item.CreatedAtUtc.ShouldBe(ticket.CreatedAtUtc);
        item.DueAtUtc.ShouldBe(ticket.DueAtUtc);
        item.SlaStatus.ShouldBe(TicketSlaStatus.AtRisk);
    }

    [Fact]
    public async Task Handle_Should_ReturnRequestedStatuses_WhenStatusesAreInformed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-1));
        Ticket closed = await SeedTicketAsync(context, requester, sector, TicketStatus.Closed, NowUtc.AddHours(-20), NowUtc.AddHours(-19));

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(
            CreateQuery(statuses: [TicketStatus.Closed]),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        TicketSummaryResponse item = result.Value.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(closed.Id);
        item.SlaStatus.ShouldBe(TicketSlaStatus.Met);
    }

    [Theory]
    [InlineData(TicketSlaStatus.OnTime)]
    [InlineData(TicketSlaStatus.AtRisk)]
    [InlineData(TicketSlaStatus.Breached)]
    [InlineData(TicketSlaStatus.Met)]
    [InlineData(TicketSlaStatus.Missed)]
    [InlineData(TicketSlaStatus.NotApplicable)]
    public async Task Handle_Should_ReturnOnlyTicketsInSlaStatus_WhenSlaStatusIsInformed(TicketSlaStatus slaStatus)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-1));
        await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-3.5));
        await SeedTicketAsync(context, requester, sector, TicketStatus.InProgress, NowUtc.AddHours(-5));
        await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-30), NowUtc.AddHours(-28));
        await SeedTicketAsync(context, requester, sector, TicketStatus.Closed, NowUtc.AddHours(-30), NowUtc.AddHours(-20));
        await SeedTicketAsync(context, requester, sector, TicketStatus.Canceled, NowUtc.AddHours(-30));

        GetTicketsQueryHandler handler = CreateHandler(context);
        TicketStatus[] allStatuses = Enum.GetValues<TicketStatus>();

        // Act
        Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(
            CreateQuery(statuses: allStatuses, slaStatus: slaStatus),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        TicketSummaryResponse item = result.Value.Items.ShouldHaveSingleItem();
        item.SlaStatus.ShouldBe(slaStatus);
    }

    [Fact]
    public async Task Handle_Should_FilterBySectorAndTitle_WhenFiltersAreInformed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        (_, Sector otherSector) = await SeedRequesterAndSectorAsync(context);
        Ticket printer = await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-1), title: "Printer jammed");
        await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-1), title: "VPN down");
        await SeedTicketAsync(context, requester, otherSector, TicketStatus.Open, NowUtc.AddHours(-1), title: "Printer offline");

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(
            CreateQuery(sectorId: sector.Id, search: "Printer"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(printer.Id);
    }

    [Fact]
    public async Task Handle_Should_FilterByPriority_WhenPriorityIsInformed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        Ticket low = await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-1), priority: TicketPriority.Low);
        await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-1));

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(
            CreateQuery(priority: TicketPriority.Low),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        TicketSummaryResponse item = result.Value.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(low.Id);
        item.Priority.ShouldBe(TicketPriority.Low);
    }

    [Fact]
    public async Task Handle_Should_FilterByLocalCreationDay_WhenCreatedPeriodIsGiven()
    {
        // Arrange: 31/10 às 23h30 em São Paulo é 02h30 UTC de 01/11; 01/11 às 10h é 13h UTC
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        await SeedTicketAsync(context, requester, sector, TicketStatus.Open, new DateTime(2026, 11, 1, 2, 30, 0, DateTimeKind.Utc));
        Ticket morning = await SeedTicketAsync(context, requester, sector, TicketStatus.Open, new DateTime(2026, 11, 1, 13, 0, 0, DateTimeKind.Utc));
        Ticket later = await SeedTicketAsync(context, requester, sector, TicketStatus.Open, new DateTime(2026, 11, 15, 13, 0, 0, DateTimeKind.Utc));

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> oneDay = await handler.Handle(
            CreateQuery(createdFrom: new DateOnly(2026, 11, 1), createdTo: new DateOnly(2026, 11, 1)),
            CancellationToken.None);
        Result<PagedResponse<TicketSummaryResponse>> fromOnly = await handler.Handle(
            CreateQuery(createdFrom: new DateOnly(2026, 11, 1)),
            CancellationToken.None);

        // Assert
        oneDay.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(morning.Id);
        fromOnly.Value.Items.Select(t => t.Id).ShouldBe([morning.Id, later.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Handle_Should_ExcludeNeverResolvedTickets_WhenResolvedPeriodIsGiven()
    {
        // Arrange: resolvido em 02/11 às 12h de São Paulo (15h UTC)
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        Ticket resolved = await SeedTicketAsync(
            context,
            requester,
            sector,
            TicketStatus.Resolved,
            new DateTime(2026, 11, 1, 13, 0, 0, DateTimeKind.Utc),
            resolvedAtUtc: new DateTime(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc));
        await SeedTicketAsync(context, requester, sector, TicketStatus.Open, new DateTime(2026, 11, 1, 14, 0, 0, DateTimeKind.Utc));

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(
            CreateQuery(resolvedFrom: new DateOnly(2026, 11, 2), resolvedTo: new DateOnly(2026, 11, 2)),
            CancellationToken.None);

        // Assert
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(resolved.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_Should_ReturnInvalidPeriod_WhenStartIsAfterEnd(bool createdPeriod)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        GetTicketsQueryHandler handler = CreateHandler(context);
        var start = new DateOnly(2026, 11, 10);
        var end = new DateOnly(2026, 11, 1);
        GetTicketsQuery query = createdPeriod
            ? CreateQuery(createdFrom: start, createdTo: end)
            : CreateQuery(resolvedFrom: start, resolvedTo: end);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.InvalidPeriod);
    }

    [Theory]
    [InlineData(TicketWaitingOn.Requester)]
    [InlineData(TicketWaitingOn.Agent)]
    public async Task Handle_Should_ReturnOnlyTicketsWaitingOnTheGivenParty_WhenWaitingOnIsGiven(TicketWaitingOn waitingOn)
    {
        // Arrange: um chamado aguardando o solicitante, um aguardando quem atende e um resolvido, sem situação
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        Ticket waitingOnRequester = await SeedTicketAsync(context, requester, sector, TicketStatus.InProgress, NowUtc.AddHours(-1));
        Ticket waitingOnAgent = await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-2));
        Ticket resolved = await SeedTicketAsync(context, requester, sector, TicketStatus.Resolved, NowUtc.AddHours(-3), resolvedAtUtc: NowUtc);
        // A situação é calculada pelo banco; aqui entra pelo EF.
        context.Entry(waitingOnRequester).Property(t => t.WaitingOn).CurrentValue = TicketWaitingOn.Requester;
        context.Entry(waitingOnAgent).Property(t => t.WaitingOn).CurrentValue = TicketWaitingOn.Agent;
        await context.SaveChangesAsync();

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> filtered = await handler.Handle(CreateQuery(waitingOn: waitingOn), CancellationToken.None);
        Result<PagedResponse<TicketSummaryResponse>> all = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert: só a situação pedida, e a situação vem em cada item (nula no inativo)
        Guid expectedId = waitingOn == TicketWaitingOn.Requester ? waitingOnRequester.Id : waitingOnAgent.Id;
        TicketSummaryResponse item = filtered.Value.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(expectedId);
        item.WaitingOn.ShouldBe(waitingOn);
        all.Value.Items.Single(t => t.Id == resolved.Id).WaitingOn.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnTicketsOfEverySector_WhenSeveralSectorsAreGiven()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector helpdesk) = await SeedRequesterAndSectorAsync(context);
        (_, Sector infra) = await SeedRequesterAndSectorAsync(context);
        (_, Sector other) = await SeedRequesterAndSectorAsync(context);
        Ticket inHelpdesk = await SeedTicketAsync(context, requester, helpdesk, TicketStatus.Open, NowUtc.AddHours(-1));
        Ticket inInfra = await SeedTicketAsync(context, requester, infra, TicketStatus.Open, NowUtc.AddHours(-2));
        await SeedTicketAsync(context, requester, other, TicketStatus.Open, NowUtc.AddHours(-3));

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> both = await handler.Handle(
            CreateQuery(sectorIds: [helpdesk.Id, infra.Id]),
            CancellationToken.None);
        Result<PagedResponse<TicketSummaryResponse>> one = await handler.Handle(
            CreateQuery(sectorIds: [helpdesk.Id]),
            CancellationToken.None);

        // Assert
        both.Value.Items.Select(t => t.Id).ShouldBe([inHelpdesk.Id, inInfra.Id], ignoreOrder: true);
        one.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(inHelpdesk.Id);
    }

    private static GetTicketsQueryHandler CreateHandler(TestDbContext context)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(NowUtc);

        return new GetTicketsQueryHandler(context, dateTimeProvider);
    }

    private static GetTicketsQuery CreateQuery(
        TicketStatus[]? statuses = null,
        TicketSlaStatus? slaStatus = null,
        Guid? sectorId = null,
        string? search = null,
        Guid? requesterId = null,
        Guid? assignedToUserId = null,
        bool unassignedOnly = false,
        TicketSortOrder sort = TicketSortOrder.Newest,
        TicketPriority? priority = null,
        Guid[]? sectorIds = null,
        DateOnly? createdFrom = null,
        DateOnly? createdTo = null,
        DateOnly? resolvedFrom = null,
        DateOnly? resolvedTo = null,
        TicketWaitingOn? waitingOn = null) =>
        new(
            statuses ?? [],
            new TicketScopeFilter(
                sectorIds ?? (sectorId is null ? [] : [sectorId.Value]),
                priority,
                requesterId,
                assignedToUserId,
                unassignedOnly,
                createdFrom,
                createdTo,
                resolvedFrom,
                resolvedTo),
            slaStatus,
            waitingOn,
            search,
            sort,
            new PageRequest(1, 20));

    private static async Task<(User Requester, Sector Sector)> SeedRequesterAndSectorAsync(TestDbContext context)
    {
        User requester = CreateUser();
        var sector = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), $"Sector-{Guid.NewGuid():N}", description: null);

        context.Users.Add(requester);
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        return (requester, sector);
    }

    // Chamado aberto no instante informado, por padrão de prioridade Urgente (4 h de SLA, alerta em 3 h).
    private static async Task<Ticket> SeedTicketAsync(
        TestDbContext context,
        User requester,
        Sector sector,
        TicketStatus status,
        DateTime createdAtUtc,
        DateTime? resolvedAtUtc = null,
        string title = "Printer is broken",
        Guid? assignedToUserId = null,
        TicketPriority priority = TicketPriority.Urgent)
    {
        // Ativo com data de resolução: resolvido nessa data e reaberto depois.
        bool reopened = status is TicketStatus.Open or TicketStatus.InProgress && resolvedAtUtc is not null;

        Ticket ticket = TicketTestData.Create(
            requesterId: requester.Id,
            sector: sector,
            status: reopened ? TicketStatus.Resolved : status,
            createdAtUtc: createdAtUtc,
            assignedToUserId: assignedToUserId,
            priority: priority,
            title: title,
            description: "Original description",
            dueAtUtc: createdAtUtc.AddMinutes(ResolutionMinutes(priority)),
            slaWarningAtUtc: createdAtUtc.AddMinutes(ResolutionMinutes(priority) * 0.75),
            resolvedAtUtc: resolvedAtUtc);

        if (reopened)
        {
            ticket.Reopen([], ContinuousSlaCalendar.Create(), lastMessageIsFromAgent: false, requester.Id, resolvedAtUtc!.Value);
            ticket.ClearDomainEvents();
        }

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket;
    }

    // Meta de resolução do plano legado da prioridade, em horas corridas.
    private static int ResolutionMinutes(TicketPriority priority) =>
        TicketSlaPlan.Legacy(priority).ResolutionMinutes;
}
