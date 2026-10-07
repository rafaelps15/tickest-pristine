using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Tickets.GetSummaryByUser;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Tickets;

public sealed class GetTicketSummaryByUserQueryHandlerTests : BaseHandlerTest
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenQueryingAnotherUserWithoutManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new GetTicketSummaryByUserQueryHandler(context, userContext, permissionProvider);

        // Act
        Result<List<TicketCountResponse>> result = await handler.Handle(new GetTicketSummaryByUserQuery(OwnerId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_CountOwnTicketsByPriorityAndStatus_WhenCallerHasViewOwnPermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        SeedTicket(context, OwnerId, TicketPriority.High, TicketStatus.Open);
        SeedTicket(context, OwnerId, TicketPriority.High, TicketStatus.Open);
        SeedTicket(context, OwnerId, TicketPriority.Low, TicketStatus.Closed);
        SeedTicket(context, Guid.NewGuid(), TicketPriority.High, TicketStatus.Open);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(OwnerId, PermissionCodes.Tickets.ViewOwn, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new GetTicketSummaryByUserQueryHandler(context, userContext, permissionProvider);

        // Act
        Result<List<TicketCountResponse>> result = await handler.Handle(new GetTicketSummaryByUserQuery(OwnerId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value.ShouldContain(c => c.Priority == TicketPriority.High && c.Status == TicketStatus.Open && c.Count == 2);
        result.Value.ShouldContain(c => c.Priority == TicketPriority.Low && c.Status == TicketStatus.Closed && c.Count == 1);
    }

    private static void SeedTicket(TestDbContext context, Guid ownerId, TicketPriority priority, TicketStatus status) =>
        context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = priority,
            Status = status,
            CreatedByUserId = ownerId,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        });
}
