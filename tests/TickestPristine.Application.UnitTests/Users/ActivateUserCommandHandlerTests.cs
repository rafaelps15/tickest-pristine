using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Users.Activate;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class ActivateUserCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new ActivateUserCommandHandler(context, Substitute.For<IUserContext>(), permissionProvider);
        var command = new ActivateUserCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(command.UserId));
        await permissionProvider.DidNotReceive().InvalidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnAlreadyActive_WhenUserIsActive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: true);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new ActivateUserCommandHandler(context, Substitute.For<IUserContext>(), permissionProvider);

        // Act
        Result result = await handler.Handle(new ActivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.AlreadyActive);
        await permissionProvider.DidNotReceive().InvalidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ActivateUserAndInvalidatePermissions_WhenUserIsDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: false);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new ActivateUserCommandHandler(context, Substitute.For<IUserContext>(), permissionProvider);

        using var requestCancellation = new CancellationTokenSource();

        // Act
        Result result = await handler.Handle(new ActivateUserCommand(userId), requestCancellation.Token);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        User user = await context.Users.SingleAsync(u => u.Id == userId);
        user.IsActive.ShouldBeTrue();
        user.DeactivatedAtUtc.ShouldBeNull();
        user.DomainEvents.ShouldContain(domainEvent => domainEvent is UserActivatedDomainEvent);

        await permissionProvider.Received(1).InvalidateAsync(userId, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_Should_ReturnCannotManagePrivilegedUser_WhenUserHasAdministrativePermissionCallerLacks()
    {
        // Arrange: o usuário desativado tem uma permissão administrativa que quem chama não tem
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: false);
        await GrantPermissionAsync(context, userId, PermissionCodes.Roles.Manage);

        var callerId = Guid.NewGuid();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.GetForUserIdAsync(callerId, Arg.Any<CancellationToken>())
            .Returns([PermissionCodes.Users.Deactivate]);
        var handler = new ActivateUserCommandHandler(context, userContext, permissionProvider);

        // Act
        Result result = await handler.Handle(new ActivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.CannotManagePrivilegedUser([Permission.GetDisplayName(PermissionCodes.Roles.Manage)]));
        (await context.Users.SingleAsync(u => u.Id == userId)).IsActive.ShouldBeFalse();
        await permissionProvider.DidNotReceive().InvalidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static async Task GrantPermissionAsync(TestDbContext context, Guid userId, string permissionCode)
    {
        string roleName = $"Role-{Guid.NewGuid():N}";
        context.Roles.Add(CreateRole(roleName));
        context.RolePermissions.Add(new RolePermission { RoleName = roleName, PermissionCode = permissionCode });
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleName = roleName });
        await context.SaveChangesAsync();
    }

    private static async Task<Guid> SeedUserAsync(TestDbContext context, bool isActive)
    {
        User user = CreateUser(isActive);
        user.DeactivatedAtUtc = isActive ? null : new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }
}
