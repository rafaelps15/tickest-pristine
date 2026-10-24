using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Tickets.Assign;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class AssignTicketCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid AgentId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        AssignTicketCommandHandler handler = CreateHandler(context, Substitute.For<IPermissionProvider>());
        var command = new AssignTicketCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotFound(command.TicketId));
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Canceled)]
    public async Task Handle_Should_ReturnNotActive_WhenTicketIsNotActive(TicketStatus status)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User assignee = await SeedUserAsync(context);
        Guid ticketId = await SeedTicketAsync(context, status);
        AssignTicketCommandHandler handler = CreateHandler(context, PermissionProviderWithManage(assignee.Id));

        // Act
        Result result = await handler.Handle(new AssignTicketCommand(ticketId, assignee.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotActive(ticketId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUserNotFound_WhenAssigneeIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User assignee = await SeedUserAsync(context, isActive: false);
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open);
        AssignTicketCommandHandler handler = CreateHandler(context, PermissionProviderWithManage(assignee.Id));

        // Act
        Result result = await handler.Handle(new AssignTicketCommand(ticketId, assignee.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(assignee.Id));
    }

    [Fact]
    public async Task Handle_Should_ReturnAssigneeCannotManageTickets_WhenAssigneeLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User assignee = await SeedUserAsync(context);
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open);
        AssignTicketCommandHandler handler = CreateHandler(context, Substitute.For<IPermissionProvider>());

        // Act
        Result result = await handler.Handle(new AssignTicketCommand(ticketId, assignee.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.AssigneeCannotManageTickets(assignee.Id));
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).AssignedToUserId.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_AssignTicketRecordHistoryAndRaiseDomainEvent_WhenAssigneeManagesTickets()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User assignee = await SeedUserAsync(context);
        var previousAssigneeId = Guid.NewGuid();
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.InProgress, previousAssigneeId);
        AssignTicketCommandHandler handler = CreateHandler(context, PermissionProviderWithManage(assignee.Id));

        // Act
        Result result = await handler.Handle(new AssignTicketCommand(ticketId, assignee.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.AssignedToUserId.ShouldBe(assignee.Id);
        TicketReassignedDomainEvent reassigned = ticket.DomainEvents
            .OfType<TicketReassignedDomainEvent>()
            .ShouldHaveSingleItem();
        reassigned.PreviousAssignedToUserId.ShouldBe(previousAssigneeId);
        reassigned.AssignedToUserId.ShouldBe(assignee.Id);

        TicketHistory history = await context.TicketHistories.SingleAsync(h => h.TicketId == ticketId);
        history.Action.ShouldBe(TicketHistoryAction.Reassigned);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.OldValue.ShouldBe(previousAssigneeId.ToString());
        history.NewValue.ShouldBe(assignee.Id.ToString());
    }

    [Fact]
    public async Task Handle_Should_ChangeNothing_WhenAssigneeIsAlreadyTheCurrentOne()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User assignee = await SeedUserAsync(context);
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open, assignee.Id);
        AssignTicketCommandHandler handler = CreateHandler(context, PermissionProviderWithManage(assignee.Id));

        // Act
        Result result = await handler.Handle(new AssignTicketCommand(ticketId, assignee.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).DomainEvents.ShouldBeEmpty();
        (await context.TicketHistories.AnyAsync(h => h.TicketId == ticketId)).ShouldBeFalse();
    }

    private static IPermissionProvider PermissionProviderWithManage(Guid userId)
    {
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(userId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);

        return permissionProvider;
    }

    private static AssignTicketCommandHandler CreateHandler(TestDbContext context, IPermissionProvider permissionProvider)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(AgentId);

        return new AssignTicketCommandHandler(context, userContext, permissionProvider, Substitute.For<IDateTimeProvider>());
    }

    private static async Task<User> SeedUserAsync(TestDbContext context, bool isActive = true)
    {
        User user = CreateUser(isActive);

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    private static async Task<Guid> SeedTicketAsync(TestDbContext context, TicketStatus status, Guid? assignedToUserId = null)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = status,
            CreatedByUserId = Guid.NewGuid(),
            AssignedToUserId = assignedToUserId,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
