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
        AssignTicketCommandHandler handler = CreateHandler(context, PermissionProviderWithAssign());
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
        User assignee = await SeedAttendantAsync(context);
        Guid ticketId = await SeedTicketAsync(context, status);
        AssignTicketCommandHandler handler = CreateHandler(context, PermissionProviderWithAssign());

        // Act
        Result result = await handler.Handle(new AssignTicketCommand(ticketId, assignee.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotActive(ticketId));
        (await context.TicketHistories.AnyAsync(h => h.TicketId == ticketId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnUserNotFound_WhenAssigneeIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User assignee = await SeedAttendantAsync(context, isActive: false);
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open);
        AssignTicketCommandHandler handler = CreateHandler(context, PermissionProviderWithAssign());

        // Act
        Result result = await handler.Handle(new AssignTicketCommand(ticketId, assignee.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(assignee.Id));
    }

    [Fact]
    public async Task Handle_Should_ReturnUserNotFound_WhenAssigneeDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open);
        AssignTicketCommandHandler handler = CreateHandler(context, PermissionProviderWithAssign());
        var missingUserId = Guid.NewGuid();

        // Act
        Result result = await handler.Handle(new AssignTicketCommand(ticketId, missingUserId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(missingUserId));
    }

    [Fact]
    public async Task Handle_Should_ReturnAssigneeNotAttendant_WhenAssigneeLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User assignee = await SeedUserAsync(context);
        await GrantPermissionsAsync(context, assignee.Id, PermissionCodes.Tickets.Create);
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open);
        AssignTicketCommandHandler handler = CreateHandler(context, PermissionProviderWithAssign());

        // Act
        Result result = await handler.Handle(new AssignTicketCommand(ticketId, assignee.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.AssigneeNotAttendant(assignee.Id));
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).AssignedToUserId.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnAssigneeNotAttendant_WhenCachedPermissionsStillSayAssigneeManagesTickets()
    {
        // Arrange: o cache de permissões ainda diz que o responsável atende chamados, mas o banco não
        await using TestDbContext context = CreateDbContext();
        User assignee = await SeedUserAsync(context);
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open);
        IPermissionProvider permissionProvider = PermissionProviderWithAssign();
        permissionProvider.HasPermissionAsync(assignee.Id, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);
        AssignTicketCommandHandler handler = CreateHandler(context, permissionProvider);

        // Act
        Result result = await handler.Handle(new AssignTicketCommand(ticketId, assignee.Id), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(TicketErrors.AssigneeNotAttendant(assignee.Id));
    }

    [Fact]
    public async Task Handle_Should_ReturnCannotAssignToOthers_WhenCallerLacksAssignPermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User assignee = await SeedAttendantAsync(context);
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open);
        AssignTicketCommandHandler handler = CreateHandler(context, Substitute.For<IPermissionProvider>());

        // Act
        Result result = await handler.Handle(new AssignTicketCommand(ticketId, assignee.Id), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.CannotAssignToOthers);
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).AssignedToUserId.ShouldBeNull();
        (await context.TicketHistories.AnyAsync(h => h.TicketId == ticketId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_AssignToCaller_WhenCallerIsAttendantWithoutAssignPermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedAttendantAsync(context, id: AgentId);
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open);
        AssignTicketCommandHandler handler = CreateHandler(context, Substitute.For<IPermissionProvider>());

        // Act
        Result result = await handler.Handle(new AssignTicketCommand(ticketId, AgentId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).AssignedToUserId.ShouldBe(AgentId);
    }

    [Fact]
    public async Task Handle_Should_RecordAssigned_WhenTicketHasNoAssignee()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User assignee = await SeedAttendantAsync(context);
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open);
        AssignTicketCommandHandler handler = CreateHandler(context, PermissionProviderWithAssign());

        // Act
        Result result = await handler.Handle(new AssignTicketCommand(ticketId, assignee.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TicketHistory history = await context.TicketHistories.SingleAsync(h => h.TicketId == ticketId);
        history.Action.ShouldBe(TicketHistoryAction.Assigned);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.OldValue.ShouldBeNull();
        history.NewValue.ShouldBe(assignee.Id.ToString());
        history.OccurredAtUtc.ShouldBe(DefaultUtcNow);
    }

    [Fact]
    public async Task Handle_Should_AssignTicketRecordReassignedAndRaiseDomainEvent_WhenTicketHasAnotherAssignee()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        User assignee = await SeedAttendantAsync(context);
        var previousAssigneeId = Guid.NewGuid();
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.InProgress, previousAssigneeId);
        AssignTicketCommandHandler handler = CreateHandler(context, PermissionProviderWithAssign());

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
        User assignee = await SeedAttendantAsync(context);
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open, assignee.Id);
        AssignTicketCommandHandler handler = CreateHandler(context, PermissionProviderWithAssign());

        // Act
        Result result = await handler.Handle(new AssignTicketCommand(ticketId, assignee.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).DomainEvents.ShouldBeEmpty();
        (await context.TicketHistories.AnyAsync(h => h.TicketId == ticketId)).ShouldBeFalse();
    }

    /// <summary>
    /// Provedor em que quem chama tem <c>tickets:assign</c> e pode atribuir o chamado a outra pessoa.
    /// </summary>
    private static IPermissionProvider PermissionProviderWithAssign()
    {
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(AgentId, PermissionCodes.Tickets.Assign, Arg.Any<CancellationToken>())
            .Returns(true);

        return permissionProvider;
    }

    private static AssignTicketCommandHandler CreateHandler(TestDbContext context, IPermissionProvider permissionProvider)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(AgentId);

        return new AssignTicketCommandHandler(context, userContext, permissionProvider, CreateClock());
    }

    private static async Task<User> SeedUserAsync(TestDbContext context, bool isActive = true, Guid? id = null)
    {
        User user = CreateUser(isActive);
        user.Id = id ?? user.Id;

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// Usuário com uma role que tem <c>tickets:manage</c>, gravada no banco.
    /// </summary>
    private static async Task<User> SeedAttendantAsync(TestDbContext context, bool isActive = true, Guid? id = null)
    {
        User user = await SeedUserAsync(context, isActive, id);
        await GrantPermissionsAsync(context, user.Id, PermissionCodes.Tickets.Manage);

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
