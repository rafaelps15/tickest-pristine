using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Tickets.GetOwn;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class GetOwnTicketsQueryHandlerTests : BaseHandlerTest
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTime NowUtc = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_ReturnOnlyCallerTickets_WhenTicketsHaveDifferentStatuses()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1));
        await SeedTicketAsync(context, OwnerId, TicketStatus.InProgress, NowUtc.AddHours(-2));
        await SeedTicketAsync(context, OwnerId, TicketStatus.Resolved, NowUtc.AddHours(-3));
        await SeedTicketAsync(context, Guid.NewGuid(), TicketStatus.Open, NowUtc.AddHours(-1));

        GetOwnTicketsQueryHandler handler = CreateHandler(context, OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.Count.ShouldBe(3);
        result.Value.Items.ShouldAllBe(t => t.OpenedByUserId == OwnerId);
    }

    [Fact]
    public async Task Handle_Should_ReturnNewestFirst_WhenCallerHasSeveralTickets()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Ticket oldest = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-20));
        Ticket newest = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1));
        Ticket middle = await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-10));

        GetOwnTicketsQueryHandler handler = CreateHandler(context, OwnerId);

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.Value.Items.Select(t => t.Id).ShouldBe([newest.Id, middle.Id, oldest.Id]);
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyPage_WhenCallerHasNoTickets()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedTicketAsync(context, OwnerId, TicketStatus.Open, NowUtc.AddHours(-1));

        GetOwnTicketsQueryHandler handler = CreateHandler(context, Guid.NewGuid());

        // Act
        Result<PagedResponse<OwnTicketResponse>> result = await handler.Handle(CreateQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.TotalCount.ShouldBe(0);
    }

    private static GetOwnTicketsQueryHandler CreateHandler(TestDbContext context, Guid callerId)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);

        return new GetOwnTicketsQueryHandler(context, userContext);
    }

    private static GetOwnTicketsQuery CreateQuery() => new(new PageRequest(1, 20));

    private static async Task<Ticket> SeedTicketAsync(
        TestDbContext context,
        Guid openedByUserId,
        TicketStatus status,
        DateTime createdAtUtc)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = status,
            CreatedByUserId = openedByUserId,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = createdAtUtc
        };

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket;
    }
}
