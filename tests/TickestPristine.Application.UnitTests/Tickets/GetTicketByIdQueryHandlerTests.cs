using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Tickets.GetById;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class GetTicketByIdQueryHandlerTests : BaseHandlerTest
{
    private static readonly DateTime NowUtc = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        GetTicketByIdQueryHandler handler = CreateHandler(context, Guid.NewGuid(), canManage: true);
        var query = new GetTicketByIdQuery(Guid.NewGuid());

        // Act
        Result<TicketResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotFound(query.TicketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotParticipantAndLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketAsync(context);
        GetTicketByIdQueryHandler handler = CreateHandler(context, Guid.NewGuid(), canManage: false);

        // Act
        Result<TicketResponse> result = await handler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnTicketWithNamesAndSla_WhenCallerIsRequester()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketAsync(context);
        GetTicketByIdQueryHandler handler = CreateHandler(context, ticket.CreatedByUserId, canManage: false);

        // Act
        Result<TicketResponse> result = await handler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(ticket.Id);
        result.Value.OpenedByName.ShouldBe("Test User");
        result.Value.SectorName.ShouldBe("Helpdesk");
        result.Value.DueAtUtc.ShouldBe(ticket.DueAtUtc);
        result.Value.SlaStatus.ShouldBe(TicketSlaStatus.AtRisk);
    }

    [Fact]
    public async Task Handle_Should_ReturnTicket_WhenCallerHasManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketAsync(context);
        GetTicketByIdQueryHandler handler = CreateHandler(context, Guid.NewGuid(), canManage: true);

        // Act
        Result<TicketResponse> result = await handler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(ticket.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnCanCancelWithinOwnerWindow_WhenCallerIsOwner()
    {
        // Arrange: chamado aberto 10 minutos antes da consulta
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketAsync(context, NowUtc.AddMinutes(-10));
        GetTicketByIdQueryHandler handler = CreateHandler(context, ticket.CreatedByUserId, canManage: false, canEditOwn: true);

        // Act
        Result<TicketResponse> result = await handler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        // Assert
        result.Value.CanCancel.ShouldBeTrue();
        result.Value.OwnerCancelDeadlineUtc.ShouldBe(ticket.CreatedAtUtc.AddMinutes(15));
    }

    [Fact]
    public async Task Handle_Should_ReturnCanCancelOnlyForCancelPermission_WhenOwnerWindowHasPassed()
    {
        // Arrange: chamado aberto 20 minutos antes da consulta
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketAsync(context, NowUtc.AddMinutes(-20));
        GetTicketByIdQueryHandler ownerHandler = CreateHandler(context, ticket.CreatedByUserId, canManage: false, canEditOwn: true);
        GetTicketByIdQueryHandler administratorHandler = CreateHandler(context, Guid.NewGuid(), canManage: true, canCancelAny: true);

        // Act
        Result<TicketResponse> asOwner = await ownerHandler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);
        Result<TicketResponse> asAdministrator = await administratorHandler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        // Assert
        asOwner.Value.CanCancel.ShouldBeFalse();
        asAdministrator.Value.CanCancel.ShouldBeTrue();
        asOwner.Value.OwnerCancelDeadlineUtc.ShouldBe(ticket.CreatedAtUtc.AddMinutes(15));
        asAdministrator.Value.OwnerCancelDeadlineUtc.ShouldBe(ticket.CreatedAtUtc.AddMinutes(15));
    }

    [Fact]
    public async Task Handle_Should_ReturnCannotCancel_WhenTicketIsInactive()
    {
        // Arrange: chamado cancelado dentro da janela do dono
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketAsync(context, NowUtc.AddMinutes(-5), TicketStatus.Canceled);
        GetTicketByIdQueryHandler ownerHandler = CreateHandler(context, ticket.CreatedByUserId, canManage: false, canEditOwn: true);
        GetTicketByIdQueryHandler administratorHandler = CreateHandler(context, Guid.NewGuid(), canManage: true, canCancelAny: true);

        // Act
        Result<TicketResponse> asOwner = await ownerHandler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);
        Result<TicketResponse> asAdministrator = await administratorHandler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        // Assert
        asOwner.Value.CanCancel.ShouldBeFalse();
        asAdministrator.Value.CanCancel.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, TicketSlaStatus.OnTime)]
    [InlineData(179, TicketSlaStatus.OnTime)]
    [InlineData(180, TicketSlaStatus.AtRisk)]
    [InlineData(239, TicketSlaStatus.AtRisk)]
    [InlineData(240, TicketSlaStatus.Breached)]
    [InlineData(600, TicketSlaStatus.Breached)]
    public async Task Handle_Should_ReturnSlaStatusFollowingWindow_WhenTicketIsNotResolved(
        int minutesAfterCreation,
        TicketSlaStatus expected)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketAsync(context, NowUtc.AddMinutes(-minutesAfterCreation), TicketStatus.InProgress);
        GetTicketByIdQueryHandler handler = CreateHandler(context, ticket.CreatedByUserId, canManage: false);

        // Act
        Result<TicketResponse> result = await handler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        // Assert
        result.Value.SlaStatus.ShouldBe(expected);
    }

    [Theory]
    [InlineData(240, TicketSlaStatus.Met)]
    [InlineData(241, TicketSlaStatus.Missed)]
    public async Task Handle_Should_FreezeSlaStatusAtFirstResolution_WhenTicketWasResolved(
        int resolvedMinutesAfterCreation,
        TicketSlaStatus expected)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        DateTime createdAtUtc = NowUtc.AddDays(-10);
        Ticket ticket = await SeedTicketAsync(
            context,
            createdAtUtc,
            TicketStatus.Resolved,
            resolvedAtUtc: createdAtUtc.AddMinutes(resolvedMinutesAfterCreation));
        GetTicketByIdQueryHandler handler = CreateHandler(context, ticket.CreatedByUserId, canManage: false);

        // Act
        Result<TicketResponse> result = await handler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        // Assert
        result.Value.SlaStatus.ShouldBe(expected);
    }

    [Fact]
    public async Task Handle_Should_ReturnSlaNotApplicable_WhenTicketWasCanceledWithoutResolution()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketAsync(context, NowUtc.AddDays(-10), TicketStatus.Canceled);
        GetTicketByIdQueryHandler handler = CreateHandler(context, ticket.CreatedByUserId, canManage: false);

        // Act
        Result<TicketResponse> result = await handler.Handle(new GetTicketByIdQuery(ticket.Id), CancellationToken.None);

        // Assert
        result.Value.SlaStatus.ShouldBe(TicketSlaStatus.NotApplicable);
    }

    private static GetTicketByIdQueryHandler CreateHandler(
        TestDbContext context,
        Guid callerId,
        bool canManage,
        bool canEditOwn = false,
        bool canCancelAny = false)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(canManage);
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(canEditOwn);
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.Cancel, Arg.Any<CancellationToken>())
            .Returns(canCancelAny);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(NowUtc);

        return new GetTicketByIdQueryHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), dateTimeProvider);
    }

    // Chamado Urgente aberto, por padrão, 3,5 h antes de NowUtc: já passou do alerta (3 h) e não venceu
    // (4 h).
    private static async Task<Ticket> SeedTicketAsync(
        TestDbContext context,
        DateTime? openedAtUtc = null,
        TicketStatus status = TicketStatus.Open,
        DateTime? resolvedAtUtc = null)
    {
        User requester = CreateUser();
        var sector = Sector.Create(Department.Create("Suporte", "Departamento de teste", responsibleUser: null), "Helpdesk", description: null);
        DateTime createdAtUtc = openedAtUtc ?? NowUtc.AddHours(-3.5);

        Ticket ticket = TicketTestData.Create(
            requesterId: requester.Id,
            sector: sector,
            status: status,
            createdAtUtc: createdAtUtc,
            priority: TicketPriority.Urgent,
            description: "Original description",
            slaPlan: TicketSlaPlan.Legacy(TicketPriority.Urgent),
            dueAtUtc: createdAtUtc.AddHours(4),
            slaWarningAtUtc: createdAtUtc.AddHours(3),
            resolvedAtUtc: resolvedAtUtc);

        context.Users.Add(requester);
        context.Sectors.Add(sector);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket;
    }
}
