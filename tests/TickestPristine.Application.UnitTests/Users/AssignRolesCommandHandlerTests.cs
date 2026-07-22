using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users.AssignRoles;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class AssignRolesCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new AssignRolesCommandHandler(context, permissionProvider);

        var command = new AssignRolesCommand { UserId = Guid.NewGuid(), RoleIds = [Guid.NewGuid()] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenARoleDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = User.Create("user@example.com", "Test", "User");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new AssignRolesCommandHandler(context, permissionProvider);

        var command = new AssignRolesCommand { UserId = user.Id, RoleIds = [Guid.NewGuid()] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Roles.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReplaceExistingRoles_AndInvalidateUserCache()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = User.Create("user@example.com", "Test", "User");
        context.Users.Add(user);

        var oldRole = Role.Create("Requester");
        var newRole = Role.Create("Manager");
        context.Roles.Add(oldRole);
        context.Roles.Add(newRole);
        context.UserRoles.Add(UserRole.Create(user.Id, oldRole.Id));

        await context.SaveChangesAsync();

        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new AssignRolesCommandHandler(context, permissionProvider);

        var command = new AssignRolesCommand { UserId = user.Id, RoleIds = [newRole.Id] };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        List<Guid> assignedRoleIds = await context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync();
        assignedRoleIds.ShouldBe([newRole.Id]);

        await permissionProvider.Received(1).InvalidateAsync(user.Id, Arg.Any<CancellationToken>());
    }
}
