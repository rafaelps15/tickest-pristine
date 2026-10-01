using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Tickets.GetByUser;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class GetTicketsByUserQueryHandlerTests : BaseHandlerTest
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenQueryingAnotherUserWithoutManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new GetTicketsByUserQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketsByUserQuery(OwnerId);

        // Act
        Result<List<TicketResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnAllOwnerTickets_WhenTicketsHaveDifferentStatuses()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedTicketAsync(context, OwnerId, TicketStatus.Open);
        await SeedTicketAsync(context, OwnerId, TicketStatus.InProgress);
        await SeedTicketAsync(context, OwnerId, TicketStatus.Resolved);
        await SeedTicketAsync(context, Guid.NewGuid(), TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetTicketsByUserQueryHandler(context, userContext, permissionProvider);
        var query = new GetTicketsByUserQuery(OwnerId);

        // Act
        Result<List<TicketResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(3);
        result.Value.ShouldAllBe(t => t.OpenedByUserId == OwnerId);
    }

    private static async Task SeedTicketAsync(TestDbContext context, Guid openedByUserId, TicketStatus status)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            CreatedByUserId = openedByUserId,
            AssignedToUserId = null,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        if (status != TicketStatus.Open)
        {
            ticket.Status = status;
        }

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
    }
}
