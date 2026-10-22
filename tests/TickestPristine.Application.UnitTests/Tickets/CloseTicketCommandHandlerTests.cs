using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Tickets.Close;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class CloseTicketCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid AgentId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        CloseTicketCommandHandler handler = CreateHandler(context);
        var command = new CloseTicketCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.NotFound(command.TicketId));
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Canceled)]
    public async Task Handle_Should_ReturnInvalidStatusTransition_WhenTicketIsNotResolved(TicketStatus status)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, status);
        CloseTicketCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new CloseTicketCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketErrors.InvalidStatusTransition(status, TicketStatus.Closed));
        (await context.Tickets.SingleAsync(t => t.Id == ticketId)).Status.ShouldBe(status);
    }

    [Fact]
    public async Task Handle_Should_CloseTicketRecordHistoryAndRaiseDomainEvent_WhenTicketIsResolved()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Resolved);
        CloseTicketCommandHandler handler = CreateHandler(context);

        // Act
        Result result = await handler.Handle(new CloseTicketCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.Status.ShouldBe(TicketStatus.Closed);
        TicketStatusChangedDomainEvent statusChanged = ticket.DomainEvents
            .OfType<TicketStatusChangedDomainEvent>()
            .ShouldHaveSingleItem();
        statusChanged.OldStatus.ShouldBe(TicketStatus.Resolved);
        statusChanged.NewStatus.ShouldBe(TicketStatus.Closed);

        TicketHistory history = await context.TicketHistories.SingleAsync(h => h.TicketId == ticketId);
        history.Action.ShouldBe(TicketHistoryAction.StatusChanged);
        history.ChangedByUserId.ShouldBe(AgentId);
        history.OldValue.ShouldBe(nameof(TicketStatus.Resolved));
        history.NewValue.ShouldBe(nameof(TicketStatus.Closed));
    }

    private static CloseTicketCommandHandler CreateHandler(TestDbContext context)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(AgentId);

        return new CloseTicketCommandHandler(context, userContext, Substitute.For<IDateTimeProvider>());
    }

    private static async Task<Guid> SeedTicketAsync(TestDbContext context, TicketStatus status)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = status,
            CreatedByUserId = Guid.NewGuid(),
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
