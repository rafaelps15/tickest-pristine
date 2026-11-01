using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Tickets.GetOwnSummary;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class GetOwnTicketSummaryQueryHandlerTests : BaseHandlerTest
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTime NowUtc = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_CountOnlyCallerTicketsByPriorityAndStatus_WhenOtherUsersHaveTickets()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        SeedTicket(context, OwnerId, TicketPriority.High, TicketStatus.Open);
        SeedTicket(context, OwnerId, TicketPriority.High, TicketStatus.Open);
        SeedTicket(context, OwnerId, TicketPriority.Low, TicketStatus.Closed);
        SeedTicket(context, Guid.NewGuid(), TicketPriority.High, TicketStatus.Open);
        await context.SaveChangesAsync();

        GetOwnTicketSummaryQueryHandler handler = CreateHandler(context, OwnerId);

        // Act
        Result<List<OwnTicketCountResponse>> result = await handler.Handle(new GetOwnTicketSummaryQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value.ShouldContain(c => c.Priority == TicketPriority.High && c.Status == TicketStatus.Open && c.Count == 2);
        result.Value.ShouldContain(c => c.Priority == TicketPriority.Low && c.Status == TicketStatus.Closed && c.Count == 1);
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyList_WhenCallerHasNoTickets()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        SeedTicket(context, OwnerId, TicketPriority.High, TicketStatus.Open);
        await context.SaveChangesAsync();

        GetOwnTicketSummaryQueryHandler handler = CreateHandler(context, Guid.NewGuid());

        // Act
        Result<List<OwnTicketCountResponse>> result = await handler.Handle(new GetOwnTicketSummaryQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    private static GetOwnTicketSummaryQueryHandler CreateHandler(TestDbContext context, Guid callerId)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);

        return new GetOwnTicketSummaryQueryHandler(context, userContext);
    }

    private static void SeedTicket(TestDbContext context, Guid ownerId, TicketPriority priority, TicketStatus status) =>
        context.Tickets.Add(TicketTestData.Create(
            requesterId: ownerId,
            status: status,
            createdAtUtc: NowUtc,
            priority: priority,
            description: "Original description"));
}
