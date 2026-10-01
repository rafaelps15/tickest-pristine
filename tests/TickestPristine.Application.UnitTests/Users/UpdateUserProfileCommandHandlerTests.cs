using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Users.UpdateProfile;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class UpdateUserProfileCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotSelfAndLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "someone@example.com",
            FirstName = "Some",
            LastName = "One",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        var callerId = Guid.NewGuid();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Users.Update, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new UpdateUserProfileCommandHandler(context, userContext, permissionProvider);
        var command = new UpdateUserProfileCommand(user.Id, "New", "Name");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var userId = Guid.NewGuid();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(userId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new UpdateUserProfileCommandHandler(context, userContext, permissionProvider);
        var command = new UpdateUserProfileCommand(userId, "New", "Name");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.NotFound(command.UserId));
    }

    [Fact]
    public async Task Handle_Should_UpdateProfileAndRaiseDomainEvent_WhenCallerIsSelf()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "someone@example.com",
            FirstName = "Some",
            LastName = "One",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        user.ClearDomainEvents();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(user.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new UpdateUserProfileCommandHandler(context, userContext, permissionProvider);
        var command = new UpdateUserProfileCommand(user.Id, "New", "Name");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        User updated = await context.Users.SingleAsync(u => u.Id == user.Id);
        updated.FirstName.ShouldBe("New");
        updated.LastName.ShouldBe("Name");
        updated.DomainEvents.ShouldContain(domainEvent => domainEvent is UserProfileUpdatedDomainEvent);

        await permissionProvider.DidNotReceive().HasPermissionAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_UpdateProfile_WhenCallerHasManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "someone@example.com",
            FirstName = "Some",
            LastName = "One",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        var adminId = Guid.NewGuid();
        userContext.UserId.Returns(adminId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(adminId, PermissionCodes.Users.Update, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new UpdateUserProfileCommandHandler(context, userContext, permissionProvider);
        var command = new UpdateUserProfileCommand(user.Id, "New", "Name");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        User updated = await context.Users.SingleAsync(u => u.Id == user.Id);
        updated.FirstName.ShouldBe("New");
        updated.LastName.ShouldBe("Name");
    }

    [Fact]
    public async Task Handle_Should_NotRaiseDomainEvent_WhenProfileIsUnchanged()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "someone@example.com",
            FirstName = "Some",
            LastName = "One",
            Code = $"usr_{Ulid.NewUlid()}"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        user.ClearDomainEvents();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(user.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new UpdateUserProfileCommandHandler(context, userContext, permissionProvider);
        var command = new UpdateUserProfileCommand(user.Id, "Some", "One");

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        user.DomainEvents.ShouldNotContain(domainEvent => domainEvent is UserProfileUpdatedDomainEvent);
    }
}
