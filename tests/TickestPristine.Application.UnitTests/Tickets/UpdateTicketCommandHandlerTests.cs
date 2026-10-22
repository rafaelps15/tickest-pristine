using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Domain.Roles;
using TickestPristine.Application.Tickets.Update;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class UpdateTicketCommandHandlerTests : BaseHandlerTest
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

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new UpdateTicketCommand { TicketId = Guid.NewGuid(), Description = "Updated description", Status = TicketStatus.Open };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotFound(command.TicketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenNonOwnerLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.Open };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenOwnerLacksUpdateOwnPermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.InProgress };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
        (await context.TicketHistories.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotActive_WhenTicketIsResolved()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Resolved);
        UpdateTicketCommandHandler handler = CreateManagerHandler(context, DateTime.UtcNow);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.Closed };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotActive(ticketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenNonOwnerLacksManagePermissionAndTicketIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Resolved);

        IUserContext userContext = Substitute.For<IUserContext>();
        var outsiderId = Guid.NewGuid();
        userContext.UserId.Returns(outsiderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(outsiderId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.Closed };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidStatusTransition_WhenTransitionIsNotAllowed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.Closed };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.InvalidStatusTransition(TicketStatus.Open, TicketStatus.Closed));
        result.Error.Description.ShouldContain("'Aberto' para 'Fechado'");
    }

    [Fact]
    public async Task Handle_Should_UpdateTicket_WhenOwnerHasUpdateOwnPermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.InProgress };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.Description.ShouldBe("Updated description");
        ticket.Status.ShouldBe(TicketStatus.InProgress);
        ticket.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketUpdatedDomainEvent);
        ticket.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketStatusChangedDomainEvent);
        (await context.TicketHistories.SingleAsync(h => h.TicketId == ticket.Id)).Action.ShouldBe(TicketHistoryAction.StatusChanged);
    }

    [Fact]
    public async Task Handle_Should_NotRaiseStatusChangedEvent_WhenStatusIsUnchanged()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.UpdateOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new UpdateTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Updated description", Status = TicketStatus.Open };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketUpdatedDomainEvent);
        ticket.DomainEvents.ShouldNotContain(domainEvent => domainEvent is TicketStatusChangedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_RecordResolutionTime_WhenTicketIsResolvedForTheFirstTime()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.InProgress);
        var resolvedAtUtc = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

        UpdateTicketCommandHandler handler = CreateManagerHandler(context, resolvedAtUtc);
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Fixed", Status = TicketStatus.Resolved };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.ResolvedAtUtc.ShouldBe(resolvedAtUtc);
    }

    [Fact]
    public async Task Handle_Should_KeepFirstResolutionTime_WhenReopenedTicketIsResolvedAgain()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.InProgress);
        var firstResolutionUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        Ticket seeded = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        seeded.ResolvedAtUtc = firstResolutionUtc;
        await context.SaveChangesAsync();

        UpdateTicketCommandHandler handler = CreateManagerHandler(context, firstResolutionUtc.AddDays(4));
        var command = new UpdateTicketCommand { TicketId = ticketId, Description = "Fixed again", Status = TicketStatus.Resolved };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.ResolvedAtUtc.ShouldBe(firstResolutionUtc);
    }

    private static UpdateTicketCommandHandler CreateManagerHandler(TestDbContext context, DateTime nowUtc)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(nowUtc);

        return new UpdateTicketCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
    }

    private static async Task<Guid> SeedTicketAsync(TestDbContext context, Guid openedByUserId, TicketStatus status)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            CreatedByUserId = openedByUserId,
            AssignedToUserId = null,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        if (status != TicketStatus.Open)
        {
            ticket.Status = status;
        }

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
