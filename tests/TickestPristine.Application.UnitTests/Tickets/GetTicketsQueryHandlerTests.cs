using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Tickets.GetAll;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class GetTicketsQueryHandlerTests : BaseHandlerTest
{
    private static readonly DateTime NowUtc = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_ReturnOnlyActiveTicketsOrderedByDueDate_WhenNoStatusIsInformed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        (User requester, Sector sector) = await SeedRequesterAndSectorAsync(context);
        Ticket later = await SeedTicketAsync(context, requester, sector, TicketStatus.Open, NowUtc.AddHours(-1));
        Ticket breached = await SeedTicketAsync(context, requester, sector, TicketStatus.InProgress, NowUtc.AddHours(-10));
        await SeedTicketAsync(context, requester, sector, TicketStatus.Closed, NowUtc.AddHours(-20), NowUtc.AddHours(-19));

        GetTicketsQueryHandler handler = CreateHandler(context);

        // Act
        Result<PagedResponse<TicketSummaryResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Select(t => t.Id).ShouldBe([breached.Id, later.Id]);
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
        result.Value.Items.ShouldHaveSingleItem().Id.ShouldBe(printer.Id);
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
        string? search = null) =>
        new(statuses ?? [], null, sectorId, slaStatus, search, new PageRequest(1, 20));

    private static async Task<(User Requester, Sector Sector)> SeedRequesterAndSectorAsync(TestDbContext context)
    {
        User requester = CreateUser();
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            Name = $"Sector-{Guid.NewGuid():N}",
            IsActive = true,
            DepartmentId = Guid.NewGuid()
        };

        context.Users.Add(requester);
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        return (requester, sector);
    }

    /// <summary>
    /// Chamado de prioridade Urgente (4 h de SLA, alerta em 3 h) aberto no instante informado.
    /// </summary>
    private static async Task<Ticket> SeedTicketAsync(
        TestDbContext context,
        User requester,
        Sector sector,
        TicketStatus status,
        DateTime createdAtUtc,
        DateTime? resolvedAtUtc = null,
        string title = "Printer is broken")
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "Original description",
            Priority = TicketPriority.Urgent,
            Status = status,
            CreatedByUserId = requester.Id,
            SectorId = sector.Id,
            CreatedAtUtc = createdAtUtc,
            DueAtUtc = TicketSla.DueAt(createdAtUtc, TicketPriority.Urgent),
            SlaWarningAtUtc = TicketSla.WarningAt(createdAtUtc, TicketPriority.Urgent),
            ResolvedAtUtc = resolvedAtUtc
        };

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket;
    }
}
