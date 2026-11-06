using System.Data.Common;
using NSubstitute;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.Infrastructure.Database;
using TickestPristine.IntegrationTests.Abstractions;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.Tickets;

public sealed class TicketViewAccessTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    private static readonly Guid RequesterId = Guid.NewGuid();
    private static readonly Guid AssigneeId = Guid.NewGuid();

    [Fact]
    public async Task EnsureCanViewAsync_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        // Act
        Result result = await EnsureCanViewAsync(callerId, canManage: true, ticketId);

        // Assert
        result.Error.ShouldBe(TicketErrors.NotFound(ticketId));
    }

    [Fact]
    public async Task EnsureCanViewAsync_Should_ReturnNotFound_WhenTicketIsDeleted()
    {
        // Arrange
        Guid ticketId = await SeedTicketAsync(deletedAtUtc: DefaultUtcNow);

        // Act
        Result result = await EnsureCanViewAsync(RequesterId, canManage: true, ticketId);

        // Assert
        result.Error.ShouldBe(TicketErrors.NotFound(ticketId));
    }

    [Fact]
    public async Task EnsureCanViewAsync_Should_Succeed_WhenCallerIsRequesterOrAssignee()
    {
        // Arrange
        Guid ticketId = await SeedTicketAsync();

        // Act
        Result asRequester = await EnsureCanViewAsync(RequesterId, canManage: false, ticketId);
        Result asAssignee = await EnsureCanViewAsync(AssigneeId, canManage: false, ticketId);

        // Assert
        asRequester.IsSuccess.ShouldBeTrue();
        asAssignee.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureCanViewAsync_Should_ReturnUnauthorized_WhenOutsiderLacksManagePermission()
    {
        // Arrange
        Guid ticketId = await SeedTicketAsync();
        var outsiderId = Guid.NewGuid();

        // Act
        Result result = await EnsureCanViewAsync(outsiderId, canManage: false, ticketId);

        // Assert
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task EnsureCanViewAsync_Should_Succeed_WhenOutsiderCanManageTickets()
    {
        // Arrange
        Guid ticketId = await SeedTicketAsync();
        var managerId = Guid.NewGuid();

        // Act
        Result result = await EnsureCanViewAsync(managerId, canManage: true, ticketId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    private async Task<Result> EnsureCanViewAsync(Guid callerId, bool canManage, Guid ticketId)
    {
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(canManage);
        var access = new TicketViewAccess(permissionProvider);

        await using DbConnection connection = await DbConnectionFactory.OpenConnectionAsync();

        return await access.EnsureCanViewAsync(connection, callerId, ticketId, CancellationToken.None);
    }

    private async Task<Guid> SeedTicketAsync(DateTime? deletedAtUtc = null)
    {
        Ticket ticket = QueryTicketTestData.Create(
            requesterId: RequesterId,
            assignedToUserId: AssigneeId,
            description: "Original description",
            deletedAtUtc: deletedAtUtc);

        await using ApplicationDbContext context = CreateDbContext();
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
