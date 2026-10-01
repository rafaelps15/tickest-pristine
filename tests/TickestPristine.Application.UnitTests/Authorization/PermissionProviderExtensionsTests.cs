using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Authorization;

namespace TickestPristine.Application.UnitTests.Authorization;

public sealed class PermissionProviderExtensionsTests
{
    [Fact]
    public async Task IsSelfOrHasPermissionAsync_Should_ReturnTrueWithoutCheckingPermission_WhenCallerIsTarget()
    {
        // Arrange
        var userId = Guid.NewGuid();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();

        // Act
        bool allowed = await permissionProvider.IsSelfOrHasPermissionAsync(userId, userId, PermissionCodes.Users.Update);

        // Assert
        allowed.ShouldBeTrue();
        await permissionProvider.DidNotReceive().HasPermissionAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsSelfOrHasPermissionAsync_Should_DependOnPermission_WhenCallerIsNotTarget(bool hasPermission)
    {
        // Arrange
        var callerId = Guid.NewGuid();
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(callerId, PermissionCodes.Users.Update, Arg.Any<CancellationToken>())
            .Returns(hasPermission);

        // Act
        bool allowed = await permissionProvider.IsSelfOrHasPermissionAsync(callerId, Guid.NewGuid(), PermissionCodes.Users.Update);

        // Assert
        allowed.ShouldBe(hasPermission);
    }
}
