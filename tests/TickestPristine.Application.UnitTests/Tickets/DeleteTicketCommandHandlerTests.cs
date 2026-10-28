using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Tickets.Delete;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class DeleteTicketCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new DeleteTicketCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotFound(command.TicketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotActive_WhenTicketIsAlreadyInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Resolved);
        var administratorId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(administratorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(administratorId, PermissionCodes.Tickets.Delete, Arg.Any<CancellationToken>())
            .Returns(true);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new DeleteTicketCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotActive(ticketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerLacksDeletePermissionAndTicketIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Resolved);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new DeleteTicketCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorizedAndKeepTicket_WhenOwnerLacksDeletePermission()
    {
        // Arrange: o dono pode editar o próprio chamado, mas não tem tickets:delete
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(true);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new DeleteTicketCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).DeletedAtUtc.ShouldBeNull();
        (await context.TicketHistories.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorizedAndKeepTicket_WhenManagerLacksDeletePermission()
    {
        // Arrange: quem atende a fila (tickets:manage) também precisa de tickets:delete
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        var managerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(managerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(managerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new DeleteTicketCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).DeletedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_SoftDeleteTicketAndRaiseDomainEvent_WhenCallerHasDeletePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        var administratorId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(administratorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(administratorId, PermissionCodes.Tickets.Delete, Arg.Any<CancellationToken>())
            .Returns(true);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new DeleteTicketCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.IgnoreQueryFilters().SingleAsync(t => t.Id == ticketId);
        ticket.DeletedAtUtc.ShouldNotBeNull();
        ticket.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketDeletedDomainEvent);

        // O histórico registra quem excluiu, não o dono.
        TicketHistory history = await context.TicketHistories.SingleAsync(h => h.TicketId == ticket.Id);
        history.Action.ShouldBe(TicketHistoryAction.Deleted);
        history.ChangedByUserId.ShouldBe(administratorId);
    }

    [Fact]
    public async Task Handle_Should_DequeueSlaTimersWithoutBreachOrStop_WhenTicketIsDeleted()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedTicketWithSlaAsync(context, OwnerId, DefaultUtcNow);
        var administratorId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(administratorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(administratorId, PermissionCodes.Tickets.Delete, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new DeleteTicketCommandHandler(context, userContext, permissionProvider, CreateClock(DefaultUtcNow.AddMinutes(50)));

        // Act
        Result result = await handler.Handle(new DeleteTicketCommand(ticket.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<TicketSlaTimer> timers = await context.TicketSlaTimers.Where(t => t.TicketId == ticket.Id).ToListAsync();
        timers.Count.ShouldBe(2);
        timers.ShouldAllBe(t => t.NextActionOnUtc == null && t.BreachedOnUtc == null && t.StoppedOnUtc == null);
    }

    private static async Task<Guid> SeedTicketAsync(TestDbContext context, Guid createdByUserId, TicketStatus status)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = status,
            CreatedByUserId = createdByUserId,
            AssignedToUserId = null,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
