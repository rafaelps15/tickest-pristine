using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Users.Deactivate;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class DeactivateUserCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new DeactivateUserCommandHandler(context, CallerContext(), permissionProvider, Substitute.For<IDateTimeProvider>());
        var command = new DeactivateUserCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(command.UserId));
        await permissionProvider.DidNotReceive().InvalidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnAlreadyDeactivated_WhenUserIsAlreadyDeactivated()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: false);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new DeactivateUserCommandHandler(context, CallerContext(), permissionProvider, Substitute.For<IDateTimeProvider>());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.AlreadyDeactivated);
        await permissionProvider.DidNotReceive().InvalidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_DeactivateUserAndInvalidatePermissions_WhenUserIsActive()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: true);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var deactivatedAtUtc = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(deactivatedAtUtc);
        var handler = new DeactivateUserCommandHandler(context, CallerContext(), permissionProvider, dateTimeProvider);

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        User user = await context.Users.SingleAsync(u => u.Id == userId);
        user.IsActive.ShouldBeFalse();
        user.DeactivatedAtUtc.ShouldBe(deactivatedAtUtc);
        user.DomainEvents.ShouldContain(domainEvent => domainEvent is UserDeactivatedDomainEvent);

        await permissionProvider.Received(1).InvalidateAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnCannotDeactivateSelf_WhenCallerDeactivatesOwnAccount()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid userId = await SeedUserAsync(context, isActive: true);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new DeactivateUserCommandHandler(
            context,
            CallerContext(userId),
            permissionProvider,
            Substitute.For<IDateTimeProvider>());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.CannotDeactivateSelf);
        (await context.Users.SingleAsync(u => u.Id == userId)).IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task Handle_Should_ReturnLastAdministrator_WhenNoOtherActiveAdministratorExists(bool? otherAdministratorIsActive)
    {
        // Arrange: o outro administrador não existe (null) ou está desativado (false)
        await using TestDbContext context = CreateDbContext();
        Role administratorRole = await SeedAdministratorRoleAsync(context);
        Guid administratorId = await SeedUserAsync(context, isActive: true, administratorRole);

        if (otherAdministratorIsActive is { } isActive)
        {
            await SeedUserAsync(context, isActive, administratorRole);
        }

        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        var handler = new DeactivateUserCommandHandler(context, CallerContext(), permissionProvider, Substitute.For<IDateTimeProvider>());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(administratorId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RoleErrors.LastAdministrator);
        (await context.Roles.SingleAsync(r => r.Id == administratorRole.Id)).Version.ShouldBe(0);
        await permissionProvider.DidNotReceive().InvalidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_DeactivateAdministratorAndIncrementVersion_WhenAnotherActiveAdministratorExists()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Role administratorRole = await SeedAdministratorRoleAsync(context);
        Guid administratorId = await SeedUserAsync(context, isActive: true, administratorRole);
        await SeedUserAsync(context, isActive: true, administratorRole);

        var handler = new DeactivateUserCommandHandler(
            context,
            CallerContext(),
            Substitute.For<IPermissionProvider>(),
            Substitute.For<IDateTimeProvider>());

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(administratorId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await context.Users.SingleAsync(u => u.Id == administratorId)).IsActive.ShouldBeFalse();
        (await context.Roles.SingleAsync(r => r.Id == administratorRole.Id)).Version.ShouldBe(1);
    }

    private static IUserContext CallerContext(Guid? callerId = null)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(callerId ?? Guid.NewGuid());

        return userContext;
    }

    private static async Task<Role> SeedAdministratorRoleAsync(TestDbContext context)
    {
        var role = new Role { Id = Guid.NewGuid(), Name = "Administrador", IsAdministrator = true };
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        return role;
    }

    private static async Task<Guid> SeedUserAsync(TestDbContext context, bool isActive)
    {
        User user = CreateUser(isActive);

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }

    private static async Task<Guid> SeedUserAsync(TestDbContext context, bool isActive, Role role)
    {
        Guid userId = await SeedUserAsync(context, isActive);
        context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = userId, RoleId = role.Id });
        await context.SaveChangesAsync();

        return userId;
    }
}
