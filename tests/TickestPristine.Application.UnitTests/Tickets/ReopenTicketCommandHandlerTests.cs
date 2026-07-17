using TickestPristine.Application.Tickets.Reopen;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class ReopenTicketCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new ReopenTicketCommandHandler(context);
        var command = new ReopenTicketCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tickets.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnAlreadyActive_WhenTicketIsAlreadyActive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open);

        var handler = new ReopenTicketCommandHandler(context);
        var command = new ReopenTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tickets.AlreadyActive");
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Canceled)]
    public async Task Handle_Should_ReopenTicket_WhenTicketIsInactive(TicketStatus status)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, status);

        var handler = new ReopenTicketCommandHandler(context);
        var command = new ReopenTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.SingleAsync(t => t.Id == ticketId);
        ticket.Status.ShouldBe(TicketStatus.Open);
    }

    private static async Task<Guid> SeedTicketAsync(TestDbContext context, TicketStatus status)
    {
        var ticket = Ticket.Create(
            "Printer is broken",
            "Original description",
            TicketPriority.Medium,
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            DateTime.UtcNow);

        if (status != TicketStatus.Open)
        {
            ticket.Update(ticket.Description, status);
        }

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
