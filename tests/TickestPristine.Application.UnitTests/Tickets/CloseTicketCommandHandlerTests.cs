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
        Ticket ticket = TicketTestData.Create(status: status, description: "Original description");

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }

    // Chamado resolvido dentro do prazo: aberto às 08:10 de 26/10/2026 (São Paulo) e resolvido às 10:10.
    private static readonly DateTime SlaOpenedAtUtc = new(2026, 10, 26, 11, 10, 0, DateTimeKind.Utc);
    private static readonly DateTime SlaResolvedAtUtc = new(2026, 10, 26, 13, 10, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_NotTouchSlaTimers_WhenTicketIsClosed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket ticket = await SeedResolvedTicketWithSlaAsync(context, Guid.NewGuid(), SlaOpenedAtUtc, SlaResolvedAtUtc);
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(AgentId);

        var handler = new CloseTicketCommandHandler(context, userContext, CreateClock(SlaResolvedAtUtc.AddHours(1)));

        // Act
        Result result = await handler.Handle(new CloseTicketCommand(ticket.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<TicketSlaTimer> timers = await context.TicketSlaTimers.Where(t => t.TicketId == ticket.Id).ToListAsync();
        timers.Count.ShouldBe(2);
        timers.ShouldAllBe(t => t.CompletedOnUtc == SlaResolvedAtUtc && t.StoppedOnUtc == null);
    }
}
