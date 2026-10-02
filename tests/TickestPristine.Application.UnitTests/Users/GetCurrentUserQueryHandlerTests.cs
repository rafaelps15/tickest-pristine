using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Application.Users.GetCurrent;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class GetCurrentUserQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetCurrentUserQueryHandler(context, userContext, permissionProvider);

        // Act
        Result<CurrentUserResponse> result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(userId));
    }

    [Fact]
    public async Task Handle_Should_ReturnDeactivated_WhenUserIsInactive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "inactive@example.com",
            FirstName = "Inactive",
            LastName = "User",
            Code = "usr_inactive",
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(user.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetCurrentUserQueryHandler(context, userContext, permissionProvider);

        // Act
        Result<CurrentUserResponse> result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Deactivated);
    }

    [Fact]
    public async Task Handle_Should_ReturnUserWithRolesAndSortedPermissions_WhenUserExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "jane@example.com",
            FirstName = "Jane",
            LastName = "Doe",
            Code = "usr_test",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        var assignedRole = new Role { Id = Guid.NewGuid(), Name = "Atendente" };
        var otherRole = new Role { Id = Guid.NewGuid(), Name = "Outra" };
        context.Users.Add(user);
        context.Roles.AddRange(assignedRole, otherRole);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleId = assignedRole.Id });
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(user.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(["tickets:manage", "tickets:create"]);

        var handler = new GetCurrentUserQueryHandler(context, userContext, permissionProvider);

        // Act
        Result<CurrentUserResponse> result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(user.Id);
        result.Value.Email.ShouldBe(user.Email);
        result.Value.FirstName.ShouldBe(user.FirstName);
        result.Value.LastName.ShouldBe(user.LastName);
        CurrentUserRoleResponse role = result.Value.Roles.ShouldHaveSingleItem();
        role.Id.ShouldBe(assignedRole.Id);
        role.Name.ShouldBe(assignedRole.Name);
        result.Value.Permissions.ShouldBe(["tickets:create", "tickets:manage"]);
    }
}
