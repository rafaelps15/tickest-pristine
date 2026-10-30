using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Application.Tickets.Create;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.Domain.Users;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class CreateTicketCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static CreateTicketCommand Command => new()
    {
        Title = "Printer is broken",
        Description = "The printer on the 3rd floor is not working",
        Priority = TicketPriority.Medium,
        SectorId = Guid.NewGuid()
    };

    private static async Task<Guid> SeedSectorAsync(TestDbContext context)
    {
        var sector = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), "Helpdesk", description: null);
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        return sector.Id;
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenSectorDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedCurrentUserAsync(context);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = CreateClock();

        var handler = new CreateTicketCommandHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), dateTimeProvider);
        CreateTicketCommand command = Command;

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SectorErrors.NotFound(command.SectorId));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenSectorIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedCurrentUserAsync(context);
        var sector = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), "Helpdesk", description: null);
        sector.Deactivate();
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = CreateClock();

        var handler = new CreateTicketCommandHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), dateTimeProvider);
        CreateTicketCommand command = Command;
        command.SectorId = sector.Id;

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SectorErrors.NotFound(sector.Id));
    }

    [Fact]
    public async Task Handle_Should_OpenTicketForCurrentUser_WhenRequesterIdIsNotProvided()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedCurrentUserAsync(context);
        Guid sectorId = await SeedSectorAsync(context);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = CreateClock();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new CreateTicketCommandHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), dateTimeProvider);
        CreateTicketCommand command = Command;
        command.SectorId = sectorId;

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == result.Value);
        ticket.CreatedByUserId.ShouldBe(UserId);
        ticket.Status.ShouldBe(TicketStatus.Open);
        ticket.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketCreatedDomainEvent);
        (await context.TicketHistories.SingleAsync(h => h.TicketId == ticket.Id)).Action.ShouldBe(TicketHistoryAction.Created);
    }

    [Fact]
    public async Task Handle_Should_RecordDefaultPlanAndStartTimers_WhenTicketIsOpened()
    {
        // Arrange: segunda-feira, 26/10/2026, 08:10 em São Paulo (11:10 UTC).
        await using TestDbContext context = CreateDbContext();
        await SeedCurrentUserAsync(context);
        Guid sectorId = await SeedSectorAsync(context);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        var nowUtc = new DateTime(2026, 10, 26, 11, 10, 0, DateTimeKind.Utc);

        var handler = new CreateTicketCommandHandler(
            context,
            userContext,
            Substitute.For<IPermissionProvider>(),
            CreateSlaCalendarProvider(),
            CreateClock(nowUtc));
        CreateTicketCommand command = Command;
        command.SectorId = sectorId;
        command.Priority = TicketPriority.Urgent;

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == result.Value);
        ticket.SlaPlan.ShouldBe(new TicketSlaPlan(null, "Padrão", 1, SlaCalendarIds.Default, 30, 240, IsLegacy: false));

        List<TicketSlaTimer> timers = await context.TicketSlaTimers.Where(t => t.TicketId == ticket.Id).ToListAsync();
        timers.Count.ShouldBe(2);

        TicketSlaTimer firstResponse = timers.Single(t => t.Metric == SlaMetric.FirstResponse);
        firstResponse.Cycle.ShouldBe(1);
        firstResponse.StartedAtUtc.ShouldBe(nowUtc);
        firstResponse.WarningOnUtc.ShouldBe(new DateTime(2026, 10, 26, 11, 32, 30, DateTimeKind.Utc));
        firstResponse.DueOnUtc.ShouldBe(new DateTime(2026, 10, 26, 11, 40, 0, DateTimeKind.Utc));

        TicketSlaTimer resolution = timers.Single(t => t.Metric == SlaMetric.Resolution);
        resolution.Cycle.ShouldBe(1);
        resolution.WarningOnUtc.ShouldBe(new DateTime(2026, 10, 26, 14, 10, 0, DateTimeKind.Utc));
        resolution.DueOnUtc.ShouldBe(new DateTime(2026, 10, 26, 15, 10, 0, DateTimeKind.Utc));

        ticket.CreatedAtUtc.ShouldBe(nowUtc);
        ticket.DueAtUtc.ShouldBe(resolution.DueOnUtc);
        ticket.SlaWarningAtUtc.ShouldBe(resolution.WarningOnUtc);
        ticket.ResolvedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_SkipHolidayInFirstResponseDeadline_WhenOpenedBeforeLongWeekend()
    {
        // Arrange: sexta-feira, 30/10/2026, 17:50 em São Paulo; segunda, 02/11, é Finados.
        await using TestDbContext context = CreateDbContext();
        await SeedCurrentUserAsync(context);
        Guid sectorId = await SeedSectorAsync(context);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);

        var handler = new CreateTicketCommandHandler(
            context,
            userContext,
            Substitute.For<IPermissionProvider>(),
            CreateSlaCalendarProvider(),
            CreateClock(new DateTime(2026, 10, 30, 20, 50, 0, DateTimeKind.Utc)));
        CreateTicketCommand command = Command;
        command.SectorId = sectorId;
        command.Priority = TicketPriority.High;

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert: 10 minutos na sexta e 110 na terça, 03/11, a partir das 08:00.
        result.IsSuccess.ShouldBeTrue();

        TicketSlaTimer firstResponse = await context.TicketSlaTimers
            .SingleAsync(t => t.TicketId == result.Value && t.Metric == SlaMetric.FirstResponse);
        firstResponse.DueOnUtc.ShouldBe(new DateTime(2026, 11, 3, 12, 50, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenOpeningOnBehalfOfAnotherUserWithoutManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedCurrentUserAsync(context);
        Guid sectorId = await SeedSectorAsync(context);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(UserId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);
        IDateTimeProvider dateTimeProvider = CreateClock();

        var handler = new CreateTicketCommandHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), dateTimeProvider);
        CreateTicketCommand command = Command;
        command.SectorId = sectorId;
        command.RequesterId = Guid.NewGuid();

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_OpenTicketForRequester_WhenCallerHasManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedCurrentUserAsync(context);
        Guid sectorId = await SeedSectorAsync(context);
        Guid requesterId = await SeedUserAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(UserId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);
        IDateTimeProvider dateTimeProvider = CreateClock();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new CreateTicketCommandHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), dateTimeProvider);
        CreateTicketCommand command = Command;
        command.SectorId = sectorId;
        command.RequesterId = requesterId;

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == result.Value);
        ticket.CreatedByUserId.ShouldBe(requesterId);
        ticket.DomainEvents.OfType<TicketCreatedDomainEvent>().ShouldHaveSingleItem().OpenedByUserId.ShouldBe(UserId);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenRequesterDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedCurrentUserAsync(context);
        Guid sectorId = await SeedSectorAsync(context);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(UserId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new CreateTicketCommandHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), CreateClock());
        CreateTicketCommand command = Command;
        command.SectorId = sectorId;
        command.RequesterId = Guid.NewGuid();

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(command.RequesterId.Value));
        (await context.Tickets.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenAssignedUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedCurrentUserAsync(context);
        Guid sectorId = await SeedSectorAsync(context);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);

        var handler = new CreateTicketCommandHandler(
            context,
            userContext,
            Substitute.For<IPermissionProvider>(),
            CreateSlaCalendarProvider(),
            CreateClock());
        CreateTicketCommand command = Command;
        command.SectorId = sectorId;
        command.AssignedToUserId = Guid.NewGuid();

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(command.AssignedToUserId.Value));
        (await context.Tickets.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_AssignTicket_WhenAssignedUserExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedCurrentUserAsync(context);
        Guid sectorId = await SeedSectorAsync(context);
        Guid assigneeId = await SeedUserAsync(context);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IDateTimeProvider dateTimeProvider = CreateClock();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new CreateTicketCommandHandler(context, userContext, Substitute.For<IPermissionProvider>(), CreateSlaCalendarProvider(), dateTimeProvider);
        CreateTicketCommand command = Command;
        command.SectorId = sectorId;
        command.AssignedToUserId = assigneeId;

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == result.Value);
        ticket.AssignedToUserId.ShouldBe(assigneeId);
        ticket.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketCreatedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenAssignedUserIsDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedCurrentUserAsync(context);
        Guid sectorId = await SeedSectorAsync(context);
        // Usuário desativado conta como inexistente para receber o chamado.
        Guid assigneeId = await SeedUserAsync(context, isActive: false);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);

        var handler = new CreateTicketCommandHandler(
            context,
            userContext,
            Substitute.For<IPermissionProvider>(),
            CreateSlaCalendarProvider(),
            CreateClock());
        CreateTicketCommand command = Command;
        command.SectorId = sectorId;
        command.AssignedToUserId = assigneeId;

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(assigneeId));
        (await context.Tickets.AnyAsync()).ShouldBeFalse();
    }

    // O handler carrega o usuário logado para abrir o chamado em nome dele.
    private static async Task SeedCurrentUserAsync(TestDbContext context)
    {
        User user = CreateUser();
        user.Id = UserId;
        context.Users.Add(user);
        await context.SaveChangesAsync();
    }

    private static async Task<Guid> SeedUserAsync(TestDbContext context, bool isActive = true)
    {
        User user = CreateUser(isActive);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }

    [Fact]
    public async Task Handle_Should_ReturnCalendarNotFound_WhenPlanCalendarDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedCurrentUserAsync(context);
        Guid sectorId = await SeedSectorAsync(context);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);

        var handler = new CreateTicketCommandHandler(
            context,
            userContext,
            Substitute.For<IPermissionProvider>(),
            CreateMissingSlaCalendarProvider(),
            CreateClock());
        CreateTicketCommand command = Command;
        command.SectorId = sectorId;

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SlaCalendarErrors.NotFound(SlaCalendarIds.Default));
        (await context.Tickets.AnyAsync()).ShouldBeFalse();
        (await context.TicketSlaTimers.AnyAsync()).ShouldBeFalse();
    }
}
