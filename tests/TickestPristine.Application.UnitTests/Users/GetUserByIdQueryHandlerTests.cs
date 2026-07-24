using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Users.GetById;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Users;

public sealed class GetUserByIdQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotSelfAndLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = User.Create("someone@example.com", "Some", "One");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        var callerId = Guid.NewGuid();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Users.Manage, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new GetUserByIdQueryHandler(context, userContext, permissionProvider);
        var query = new GetUserByIdQuery(user.Id);

        // Act
        Result<UserResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.Unauthorized");
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

        var handler = new GetUserByIdQueryHandler(context, userContext, permissionProvider);
        var query = new GetUserByIdQuery(userId);

        // Act
        Result<UserResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Users.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnUser_WhenCallerIsSelf()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = User.Create("someone@example.com", "Some", "One");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(user.Id);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        var handler = new GetUserByIdQueryHandler(context, userContext, permissionProvider);
        var query = new GetUserByIdQuery(user.Id);

        // Act
        Result<UserResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Email.ShouldBe("someone@example.com");
    }

    [Fact]
    public async Task Handle_Should_ReturnUser_WhenCallerHasManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        var user = User.Create("someone@example.com", "Some", "One");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        var callerId = Guid.NewGuid();
        userContext.UserId.Returns(callerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Users.Manage, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new GetUserByIdQueryHandler(context, userContext, permissionProvider);
        var query = new GetUserByIdQuery(user.Id);

        // Act
        Result<UserResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(user.Id);
    }
}
