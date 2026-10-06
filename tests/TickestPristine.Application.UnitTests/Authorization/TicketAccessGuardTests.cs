using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Authorization;

public sealed class TicketAccessGuardTests : BaseHandlerTest
{
    private static readonly Guid RequesterId = Guid.NewGuid();
    private static readonly Guid AssigneeId = Guid.NewGuid();

    [Fact]
    public async Task EnsureCanViewAsync_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var ticketId = Guid.NewGuid();

        // Act
        Result result = await TicketAccessGuard.EnsureCanViewAsync(
            context, CreatePermissionProvider(Guid.NewGuid(), canManage: true), Guid.NewGuid(), ticketId, CancellationToken.None);

        // Assert
        result.Error.ShouldBe(TicketErrors.NotFound(ticketId));
    }

    [Fact]
    public async Task EnsureCanViewAsync_Should_Succeed_WhenCallerIsRequesterOrAssignee()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);

        // Act
        Result asRequester = await TicketAccessGuard.EnsureCanViewAsync(
            context, CreatePermissionProvider(RequesterId, canManage: false), RequesterId, ticketId, CancellationToken.None);
        Result asAssignee = await TicketAccessGuard.EnsureCanViewAsync(
            context, CreatePermissionProvider(AssigneeId, canManage: false), AssigneeId, ticketId, CancellationToken.None);

        // Assert
        asRequester.IsSuccess.ShouldBeTrue();
        asAssignee.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureCanViewAsync_Should_ReturnUnauthorized_WhenOutsiderLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);
        var outsiderId = Guid.NewGuid();

        // Act
        Result result = await TicketAccessGuard.EnsureCanViewAsync(
            context, CreatePermissionProvider(outsiderId, canManage: false), outsiderId, ticketId, CancellationToken.None);

        // Assert
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task EnsureCanViewAsync_Should_Succeed_WhenOutsiderCanManageTickets()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);
        var managerId = Guid.NewGuid();

        // Act
        Result result = await TicketAccessGuard.EnsureCanViewAsync(
            context, CreatePermissionProvider(managerId, canManage: true), managerId, ticketId, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    private static IPermissionProvider CreatePermissionProvider(Guid callerId, bool canManage)
    {
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(canManage);

        return permissionProvider;
    }

    private static async Task<Guid> SeedTicketAsync(TestDbContext context)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            CreatedByUserId = RequesterId,
            AssignedToUserId = AssigneeId,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
