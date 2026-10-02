using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Users.Deactivate;
using TickestPristine.Application.UnitTests.Abstractions;
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
        var handler = new DeactivateUserCommandHandler(context, permissionProvider);
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
        var handler = new DeactivateUserCommandHandler(context, permissionProvider);

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
        var handler = new DeactivateUserCommandHandler(context, permissionProvider);

        // Act
        Result result = await handler.Handle(new DeactivateUserCommand(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        User user = await context.Users.SingleAsync(u => u.Id == userId);
        user.IsActive.ShouldBeFalse();
        user.DomainEvents.ShouldContain(domainEvent => domainEvent is UserDeactivatedDomainEvent);

        await permissionProvider.Received(1).InvalidateAsync(userId, Arg.Any<CancellationToken>());
    }

    private static async Task<Guid> SeedUserAsync(TestDbContext context, bool isActive)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "someone@example.com",
            FirstName = "Some",
            LastName = "One",
            Code = $"usr_{Ulid.NewUlid()}",
            IsActive = isActive
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }
}
