using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Roles.AssignPermissions;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Roles;

public sealed class AssignRolePermissionsCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenRoleDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new AssignRolePermissionsCommandHandler(context, permissionProvider);

        var command = new AssignRolePermissionsCommand
        {
            RoleId = Guid.NewGuid(),
            PermissionCodes = [PermissionCodes.Tickets.Create]
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Roles.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReplaceExistingPermissions_AndInvalidateCacheForEveryUserWithThatRole()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();

        var role = Role.Create("Manager");
        context.Roles.Add(role);
        context.RolePermissions.Add(RolePermission.Create(role.Id, PermissionCodes.Tickets.DeleteOwn));

        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        context.UserRoles.Add(UserRole.Create(firstUserId, role.Id));
        context.UserRoles.Add(UserRole.Create(secondUserId, role.Id));

        await context.SaveChangesAsync();

        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new AssignRolePermissionsCommandHandler(context, permissionProvider);

        var command = new AssignRolePermissionsCommand
        {
            RoleId = role.Id,
            PermissionCodes = [PermissionCodes.Tickets.Create, PermissionCodes.Tickets.ViewOwn]
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<string> permissionCodes = await context.RolePermissions
            .Where(p => p.RoleId == role.Id)
            .Select(p => p.PermissionCode)
            .ToListAsync();
        permissionCodes.ShouldBe([PermissionCodes.Tickets.Create, PermissionCodes.Tickets.ViewOwn], ignoreOrder: true);

        await permissionProvider.Received(1).InvalidateAsync(firstUserId, Arg.Any<CancellationToken>());
        await permissionProvider.Received(1).InvalidateAsync(secondUserId, Arg.Any<CancellationToken>());
    }
}
