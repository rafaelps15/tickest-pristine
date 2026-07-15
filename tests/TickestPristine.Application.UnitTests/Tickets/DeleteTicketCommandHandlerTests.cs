using TickestPristine.Application.Tickets.Delete;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class DeleteTicketCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new DeleteTicketCommandHandler(context, dateTimeProvider);
        var command = new DeleteTicketCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tickets.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotActive_WhenTicketIsAlreadyInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Resolved);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new DeleteTicketCommandHandler(context, dateTimeProvider);
        var command = new DeleteTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tickets.NotActive");
    }

    [Fact]
    public async Task Handle_Should_SoftDeleteTicketAndRaiseDomainEvent_WhenActive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context, TicketStatus.Open);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new DeleteTicketCommandHandler(context, dateTimeProvider);
        var command = new DeleteTicketCommand(ticketId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        Ticket ticket = await context.Tickets.IgnoreQueryFilters().SingleAsync(t => t.Id == ticketId);
        ticket.IsDeleted.ShouldBeTrue();
        ticket.DeletedAtUtc.ShouldNotBeNull();
        ticket.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketDeletedDomainEvent);
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
            OpenedByUserId = Guid.NewGuid(),
            DepartmentId = Guid.NewGuid(),
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
