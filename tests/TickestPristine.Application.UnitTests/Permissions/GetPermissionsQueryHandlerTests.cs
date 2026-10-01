using TickestPristine.Application.Authorization;
using TickestPristine.Application.Permissions.GetAll;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.Permissions;

public sealed class GetPermissionsQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnEveryCatalogPermissionWithDisplayData_WhenQueried()
    {
        // Arrange
        var handler = new GetPermissionsQueryHandler();

        // Act
        Result<IReadOnlyList<PermissionResponse>> result = await handler.Handle(new GetPermissionsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(p => p.Code).ShouldBe(PermissionCodes.All);

        PermissionResponse assignRoles = result.Value.Single(p => p.Code == PermissionCodes.Users.AssignRoles);
        assignRoles.Name.ShouldBe("Alterar funções de colaboradores");
        assignRoles.Group.ShouldBe("Usuários");
        assignRoles.IsAdministrative.ShouldBeTrue();
    }
}
