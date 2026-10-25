using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Tickets.Reopen;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class ReopenTicketCommandHandlerTests : BaseHandlerTest
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

        var handler = new ReopenTicketCommandHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), CreateClock());
        var command = new ReopenTicketCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotFound(command.TicketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnAlreadyActive_WhenTicketIsAlreadyActive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.ReopenOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new ReopenTicketCommandHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), CreateClock());
        var command = new ReopenTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.AlreadyActive(ticketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidStatusTransition_WhenStatusCannotGoBackToOpen()
    {
        // Arrange: status fora do enum, que nenhuma transição aceita
        const TicketStatus unknownStatus = (TicketStatus)99;
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, unknownStatus);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.ReopenOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new ReopenTicketCommandHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), CreateClock());
        var command = new ReopenTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.InvalidStatusTransition(unknownStatus, TicketStatus.Open));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenNonOwnerLacksManagePermissionAndTicketIsActive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        var outsiderId = Guid.NewGuid();
        userContext.UserId.Returns(outsiderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(outsiderId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new ReopenTicketCommandHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), CreateClock());
        var command = new ReopenTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenOwnerLacksReopenOwnPermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Closed);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.ReopenOwn, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new ReopenTicketCommandHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), CreateClock());
        var command = new ReopenTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).Status.ShouldBe(TicketStatus.Closed);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenNonOwnerLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Closed);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new ReopenTicketCommandHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), CreateClock());
        var command = new ReopenTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Canceled)]
    public async Task Handle_Should_ReopenTicketAndRaiseDomainEvent_WhenOwnerHasReopenOwnPermission(TicketStatus status)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, status);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.ReopenOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new ReopenTicketCommandHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), CreateClock());
        var command = new ReopenTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.Status.ShouldBe(TicketStatus.Open);
        ticket.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketReopenedDomainEvent);

        // O histórico guarda o status anterior e o novo.
        TicketHistory history = await context.TicketHistories.SingleAsync(h => h.TicketId == ticket.Id);
        history.Action.ShouldBe(TicketHistoryAction.Reopened);
        history.OldValue.ShouldBe(status.ToString());
        history.NewValue.ShouldBe(TicketStatus.Open.ToString());
    }

    [Fact]
    public async Task Handle_Should_ReopenTicket_WhenNonOwnerHasManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, OwnerId, TicketStatus.Closed);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new ReopenTicketCommandHandler(context, userContext, permissionProvider, CreateSlaCalendarProvider(), CreateClock());
        var command = new ReopenTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.Status.ShouldBe(TicketStatus.Open);
        ticket.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketReopenedDomainEvent);

        TicketHistory history = await context.TicketHistories.SingleAsync(h => h.TicketId == ticket.Id);
        history.OldValue.ShouldBe(nameof(TicketStatus.Closed));
        history.NewValue.ShouldBe(nameof(TicketStatus.Open));
    }

    private static async Task<Guid> SeedTicketAsync(TestDbContext context, Guid createdByUserId, TicketStatus status)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            CreatedByUserId = createdByUserId,
            AssignedToUserId = null,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            SlaPlan = CreateSlaPlan()
        };

        if (status != TicketStatus.Open)
        {
            ticket.Status = status;
        }

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }

    // Chamado resolvido dentro do prazo: aberto às 08:10 de 26/10/2026 (São Paulo) e resolvido às 10:10.
    private static readonly DateTime SlaOpenedAtUtc = new(2026, 10, 26, 11, 10, 0, DateTimeKind.Utc);
    private static readonly DateTime SlaResolvedAtUtc = new(2026, 10, 26, 13, 10, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_StartNewResolutionCycleAndKeepFirstCycle_WhenTicketIsReopened()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedResolvedTicketWithSlaAsync(context, OwnerId, SlaOpenedAtUtc, SlaResolvedAtUtc);
        DateTime dueAtUtc = ticket.DueAtUtc;

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.ReopenOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        // 14:00 em São Paulo.
        var reopenedAtUtc = new DateTime(2026, 10, 26, 17, 0, 0, DateTimeKind.Utc);
        var handler = new ReopenTicketCommandHandler(
            context,
            userContext,
            permissionProvider,
            CreateSlaCalendarProvider(),
            CreateClock(reopenedAtUtc));

        // Act
        Result result = await handler.Handle(new ReopenTicketCommand(ticket.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<TicketSlaTimer> timers = await context.TicketSlaTimers.Where(t => t.TicketId == ticket.Id).ToListAsync();
        timers.Count.ShouldBe(3);

        TicketSlaTimer firstCycle = timers.Single(t => t.Metric == SlaMetric.Resolution && t.Cycle == 1);
        firstCycle.CompletedOnUtc.ShouldBe(SlaResolvedAtUtc);

        TicketSlaTimer secondCycle = timers.Single(t => t.Metric == SlaMetric.Resolution && t.Cycle == 2);
        secondCycle.StartedAtUtc.ShouldBe(reopenedAtUtc);
        secondCycle.TargetMinutes.ShouldBe(240);
        secondCycle.CompletedOnUtc.ShouldBeNull();

        timers.Single(t => t.Metric == SlaMetric.FirstResponse).CompletedOnUtc.ShouldBe(SlaResolvedAtUtc);

        Ticket saved = await context.Tickets.SingleAsync(t => t.Id == ticket.Id);
        saved.DueAtUtc.ShouldBe(dueAtUtc);
        saved.ResolvedAtUtc.ShouldBe(SlaResolvedAtUtc);
        TicketSla.GetStatus(saved.Status, saved.DueAtUtc, saved.SlaWarningAtUtc, saved.ResolvedAtUtc, reopenedAtUtc)
            .ShouldBe(TicketSlaStatus.Met);
    }

    [Fact]
    public async Task Handle_Should_ReturnCalendarNotFound_WhenPlanCalendarDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedResolvedTicketWithSlaAsync(context, OwnerId, SlaOpenedAtUtc, SlaResolvedAtUtc);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.ReopenOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new ReopenTicketCommandHandler(
            context,
            userContext,
            permissionProvider,
            CreateMissingSlaCalendarProvider(),
            CreateClock(SlaResolvedAtUtc.AddHours(1)));

        // Act
        Result result = await handler.Handle(new ReopenTicketCommand(ticket.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SlaCalendarErrors.NotFound(ticket.SlaPlan.CalendarId));
        (await context.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticket.Id)).Status.ShouldBe(TicketStatus.Resolved);
        (await context.TicketSlaTimers.CountAsync(t => t.TicketId == ticket.Id)).ShouldBe(2);
    }
}
