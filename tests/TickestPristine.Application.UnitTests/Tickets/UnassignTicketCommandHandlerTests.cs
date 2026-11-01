using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Tickets.Unassign;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class UnassignTicketCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid AgentId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        UnassignTicketCommandHandler handler = CreateHandler(context);
        var command = new UnassignTicketCommand(Guid.NewGuid());

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
        var assigneeId = Guid.NewGuid();
        Guid ticketId = await SeedTicketAsync(context, status, assigneeId);
        UnassignTicketCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new UnassignTicketCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotActive(ticketId));
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).AssignedToUserId.ShouldBe(assigneeId);
        (await context.TicketHistories.AnyAsync(h => h.TicketId == ticketId)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Canceled)]
    public async Task Handle_Should_ReturnNotActive_WhenTicketIsNotActiveAndHasNoAssignee(TicketStatus status)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, status, assignedToUserId: null);
        UnassignTicketCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new UnassignTicketCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotActive(ticketId));
        (await context.TicketHistories.AnyAsync(h => h.TicketId == ticketId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ChangeNothing_WhenTicketHasNoAssignee()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open, assignedToUserId: null);
        UnassignTicketCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new UnassignTicketCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).DomainEvents.ShouldBeEmpty();
        (await context.TicketHistories.AnyAsync(h => h.TicketId == ticketId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_RemoveAssigneeRecordHistoryAndRaiseDomainEvent_WhenTicketHasAssignee()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var previousAssigneeId = Guid.NewGuid();
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open, previousAssigneeId);
        UnassignTicketCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new UnassignTicketCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.AssignedToUserId.ShouldBeNull();
        TicketUnassignedDomainEvent unassigned = ticket.DomainEvents
            .OfType<TicketUnassignedDomainEvent>()
            .ShouldHaveSingleItem();
        unassigned.TicketId.ShouldBe(ticketId);
        unassigned.PreviousAssignedToUserId.ShouldBe(previousAssigneeId);
        unassigned.ChangedByUserId.ShouldBe(AgentId);

        TicketHistory history = await context.TicketHistories.SingleAsync(h => h.TicketId == ticketId);
        history.Action.ShouldBe(TicketHistoryAction.Unassigned);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.OldValue.ShouldBe(previousAssigneeId.ToString());
        history.NewValue.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_KeepStatus_WhenTicketIsInProgress()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.InProgress, Guid.NewGuid());
        UnassignTicketCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new UnassignTicketCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.Status.ShouldBe(TicketStatus.InProgress);
        ticket.AssignedToUserId.ShouldBeNull();
    }

    private static UnassignTicketCommandHandler CreateHandler(TestDbContext context)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(AgentId);

        return new UnassignTicketCommandHandler(context, userContext, Substitute.For<IDateTimeProvider>());
    }

    private static async Task<Guid> SeedTicketAsync(TestDbContext context, TicketStatus status, Guid? assignedToUserId)
    {
        Ticket ticket = TicketTestData.Create(
            status: status,
            assignedToUserId: assignedToUserId,
            description: "Original description");

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
