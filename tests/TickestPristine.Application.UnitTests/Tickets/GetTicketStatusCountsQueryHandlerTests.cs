using TickestPristine.Application.Tickets.GetStatusCounts;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class GetTicketStatusCountsQueryHandlerTests : BaseHandlerTest
{
    private static readonly DateTime NowUtc = new(2026, 10, 8, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_CountTicketsByStatus_WhenTicketsBelongToDifferentUsers()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        SeedTicket(context, Guid.NewGuid(), TicketStatus.Open);
        SeedTicket(context, Guid.NewGuid(), TicketStatus.Open);
        SeedTicket(context, Guid.NewGuid(), TicketStatus.InProgress);
        SeedTicket(context, Guid.NewGuid(), TicketStatus.Resolved);
        SeedTicket(context, Guid.NewGuid(), TicketStatus.Closed);
        SeedTicket(context, Guid.NewGuid(), TicketStatus.Canceled);
        await context.SaveChangesAsync();

        var handler = new GetTicketStatusCountsQueryHandler(context);

        // Act
        Result<TicketStatusCountsResponse> result = await handler.Handle(new GetTicketStatusCountsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(6);
        result.Value.OpenCount.ShouldBe(2);
        result.Value.InProgressCount.ShouldBe(1);
        result.Value.ResolvedCount.ShouldBe(1);
        result.Value.ClosedCount.ShouldBe(1);
        result.Value.CanceledCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_NotCountTicket_WhenTicketIsDeleted()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        SeedTicket(context, Guid.NewGuid(), TicketStatus.Open);
        SeedTicket(context, Guid.NewGuid(), TicketStatus.Open, deletedAtUtc: NowUtc);
        await context.SaveChangesAsync();

        var handler = new GetTicketStatusCountsQueryHandler(context);

        // Act
        Result<TicketStatusCountsResponse> result = await handler.Handle(new GetTicketStatusCountsQuery(), CancellationToken.None);

        // Assert
        result.Value.TotalCount.ShouldBe(1);
        result.Value.OpenCount.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnZeros_WhenThereAreNoTickets()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var handler = new GetTicketStatusCountsQueryHandler(context);

        // Act
        Result<TicketStatusCountsResponse> result = await handler.Handle(new GetTicketStatusCountsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalCount.ShouldBe(0);
        result.Value.OpenCount.ShouldBe(0);
    }

    private static void SeedTicket(TestDbContext context, Guid requesterId, TicketStatus status, DateTime? deletedAtUtc = null) =>
        context.Tickets.Add(TicketTestData.Create(
            requesterId: requesterId,
            status: status,
            createdAtUtc: NowUtc,
            description: "Original description",
            deletedAtUtc: deletedAtUtc));
}
