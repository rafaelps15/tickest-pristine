using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Tickets.GetByUser;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

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
        IPermissionService permissionService = Substitute.For<IPermissionService>();
        permissionService.HasPermissionAsync(userContext.UserId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new GetTicketsByUserQueryHandler(context, userContext, permissionService);
        var query = new GetTicketsByUserQuery(OwnerId);

        // Act
        Result<List<TicketResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.Unauthorized");
    }

    [Fact]
    public async Task Handle_Should_ReturnOnlyActiveTicketsForTheOwner()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        await SeedTicketAsync(context, OwnerId, TicketStatus.Open);
        await SeedTicketAsync(context, OwnerId, TicketStatus.InProgress);
        await SeedTicketAsync(context, OwnerId, TicketStatus.Resolved);
        await SeedTicketAsync(context, Guid.NewGuid(), TicketStatus.Open);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(OwnerId);
        IPermissionService permissionService = Substitute.For<IPermissionService>();

        var handler = new GetTicketsByUserQueryHandler(context, userContext, permissionService);
        var query = new GetTicketsByUserQuery(OwnerId);

        // Act
        Result<List<TicketResponse>> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
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
            Status = status,
            OpenedByUserId = openedByUserId,
            DepartmentId = Guid.NewGuid(),
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
    }
}
