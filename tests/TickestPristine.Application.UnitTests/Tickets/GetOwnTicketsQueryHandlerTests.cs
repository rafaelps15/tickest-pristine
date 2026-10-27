using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Tickets;
using TickestPristine.Application.Tickets.GetOwn;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class GetOwnTicketsQueryHandlerTests : BaseHandlerTest
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTime NowUtc = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_ReturnOnlyCallerTickets_WhenTicketsHaveDifferentStatuses()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1));
        await SeedTicketAsync(context, OwnerId, TicketStatus.InProgress, NowUtc.AddHours(-2));
        await SeedTicketAsync(context, OwnerId, TicketStatus.Resolved, NowUtc.AddHours(-3));
        await SeedTicketAsync(context, Guid.NewGuid(), TicketStatus.Open, NowUtc.AddHours(-1));

        GetOwnTicketsQueryHandler handler = CreateHandler(context, OwnerId);

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
        await using TestDbContext context = CreateDbContext();
        Ticket oldest = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-20));
        Ticket newest = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1));
        Ticket middle = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-10));

        GetOwnTicketsQueryHandler handler = CreateHandler(context, OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.Value.Items.Select(t => t.Id).ShouldBe([newest.Id, middle.Id, oldest.Id]);
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyPage_WhenCallerHasNoTickets()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1));

        GetOwnTicketsQueryHandler handler = CreateHandler(context, Guid.NewGuid());

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
        await using TestDbContext context = CreateDbContext();
        User ana = CreateUser();
        ana.FirstName = "Ana";
        ana.LastName = "Souza";
        var helpdesk = new Sector { Id = Guid.NewGuid(), Name = "Helpdesk", IsActive = true, DepartmentId = Guid.NewGuid() };
        context.Users.Add(ana);
        context.Sectors.Add(helpdesk);
        await context.SaveChangesAsync();

        Ticket assigned = await SeedTicketAsync(
            context,
            OwnerId,
            TicketStatus.InProgress,
            NowUtc.AddHours(-2),
            sectorId: helpdesk.Id,
            assignedToUserId: ana.Id);
        Ticket unassigned = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1), sectorId: helpdesk.Id);

        GetOwnTicketsQueryHandler handler = CreateHandler(context, OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        OwnTicketResponse withAssignee = result.Value.Items.Single(t => t.Id == assigned.Id);
        withAssignee.AssignedToUserId.ShouldBe(ana.Id);
        withAssignee.AssignedToName.ShouldBe("Ana Souza");
        withAssignee.SectorId.ShouldBe(helpdesk.Id);
        withAssignee.SectorName.ShouldBe("Helpdesk");
        withAssignee.DueAtUtc.ShouldBe(assigned.DueAtUtc);
        withAssignee.SlaStatus.ShouldBe(TicketSla.GetStatus(
            assigned.Status,
            assigned.DueAtUtc,
            assigned.SlaWarningAtUtc,
            assigned.ResolvedAtUtc,
            NowUtc));
        withAssignee.LastActivityAtUtc.ShouldBe(assigned.LastActivityAtUtc);

        result.Value.Items.Single(t => t.Id == unassigned.Id).AssignedToName.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_FilterByStatusGroup_WhenGroupIsGiven()
    {
        // Arrange: um chamado de cada status
        await using TestDbContext context = CreateDbContext();
        foreach (TicketStatus status in Enum.GetValues<TicketStatus>())
        {
            await SeedTicketAsync(context, OwnerId, status, NowUtc.AddHours(-1));
        }

        GetOwnTicketsQueryHandler handler = CreateHandler(context, OwnerId);

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
        await using TestDbContext context = CreateDbContext();
        Ticket printer = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1), title: "Impressora não imprime");
        Ticket email = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-2), title: "Acesso ao e-mail");

        GetOwnTicketsQueryHandler handler = CreateHandler(context, OwnerId);

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
        await using TestDbContext context = CreateDbContext();
        await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1), title: "Impressora não imprime");
        await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-2), title: "Acesso ao e-mail");

        GetOwnTicketsQueryHandler handler = CreateHandler(context, OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(
            CreateQuery(search: "   "),
            CancellationToken.None);

        // Assert
        result.Value.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_Should_OrderByLastActivityDescending_WhenActivityDiffersFromCreation()
    {
        // Arrange: A, B e C abertos nessa ordem; A teve atividade depois da abertura de C
        await using TestDbContext context = CreateDbContext();
        Ticket a = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-3), lastActivityAtUtc: NowUtc.AddMinutes(-5));
        Ticket b = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-2));
        Ticket c = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1));

        GetOwnTicketsQueryHandler handler = CreateHandler(context, OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.Value.Items.Select(t => t.Id).ShouldBe([a.Id, c.Id, b.Id]);
    }

    [Fact]
    public async Task Handle_Should_ReturnCanCancelOnlyWithinOwnerWindow_WhenCallerIsOwner()
    {
        // Arrange: chamado aberto às 9h00; a janela do dono vai até 9h15
        await using TestDbContext context = CreateDbContext();
        var openedAtUtc = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);
        Ticket ticket = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, openedAtUtc);

        // Act
        Result<PagedResponse<OwnTicketResponse>> at0910 = await CreateHandler(context, OwnerId, openedAtUtc.AddMinutes(10))
            .Handle(CreateQuery(), CancellationToken.None);
        Result<PagedResponse<OwnTicketResponse>> at0920 = await CreateHandler(context, OwnerId, openedAtUtc.AddMinutes(20))
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
        await using TestDbContext context = CreateDbContext();
        var openedAtUtc = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);
        await SeedTicketAsync(context, OwnerId, TicketStatus.Open, openedAtUtc);

        GetOwnTicketsQueryHandler handler = CreateHandler(context, OwnerId, openedAtUtc.AddHours(2), canCancelAny: true);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.Value.Items.ShouldHaveSingleItem().CanCancel.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnCannotCancel_WhenTicketIsInactive()
    {
        // Arrange: chamado cancelado dentro da janela, consultado por quem tem tickets:cancel
        await using TestDbContext context = CreateDbContext();
        var openedAtUtc = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);
        await SeedTicketAsync(context, OwnerId, TicketStatus.Canceled, openedAtUtc);

        GetOwnTicketsQueryHandler handler = CreateHandler(context, OwnerId, openedAtUtc.AddMinutes(5), canCancelAny: true);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.Value.Items.ShouldHaveSingleItem().CanCancel.ShouldBeFalse();
    }

    /// <summary>
    /// Handler para quem consulta, por padrão um Colaborador: pode editar os próprios chamados (tickets:update-own) e
    /// não tem tickets:cancel.
    /// </summary>
    private static GetOwnTicketsQueryHandler CreateHandler(
        TestDbContext context,
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

        return new GetOwnTicketsQueryHandler(context, userContext, permissionProvider, dateTimeProvider);
    }

    private static GetOwnTicketsQuery CreateQuery(OwnTicketStatusGroup? statusGroup = null, string? search = null) =>
        new(statusGroup, search, new PageRequest(1, 20));

    private static async Task<Ticket> SeedTicketAsync(
        TestDbContext context,
        Guid openedByUserId,
        TicketStatus status,
        DateTime createdAtUtc,
        string title = "Printer is broken",
        Guid? sectorId = null,
        Guid? assignedToUserId = null,
        DateTime? lastActivityAtUtc = null)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = status,
            CreatedByUserId = openedByUserId,
            AssignedToUserId = assignedToUserId,
            SectorId = sectorId ?? Guid.NewGuid(),
            CreatedAtUtc = createdAtUtc,
            DueAtUtc = createdAtUtc.AddHours(24),
            SlaWarningAtUtc = createdAtUtc.AddHours(18),
            // Como o interceptor faria na abertura: a última atividade começa na data de abertura.
            LastActivityAtUtc = lastActivityAtUtc ?? createdAtUtc
        };

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket;
    }
}
