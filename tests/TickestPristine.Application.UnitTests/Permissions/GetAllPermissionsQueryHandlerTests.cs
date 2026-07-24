using TickestPristine.Application.Authorization;
using TickestPristine.Application.Permissions.GetAll;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Permissions;

public sealed class GetAllPermissionsQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnAllKnownPermissionCodes()
    {
        // Arrange
        var handler = new GetAllPermissionsQueryHandler();

        // Act
        Result<IReadOnlyList<string>> result = await handler.Handle(new GetAllPermissionsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(PermissionCodes.All);
    }
}
