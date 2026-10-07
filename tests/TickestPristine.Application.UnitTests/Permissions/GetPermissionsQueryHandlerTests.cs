using TickestPristine.Domain.Roles;
using TickestPristine.Application.Permissions.GetAll;
using TickestPristine.Application.UnitTests.Abstractions;

namespace TickestPristine.Application.UnitTests.Permissions;

public sealed class GetPermissionsQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_ReturnEveryCatalogPermissionWithDisplayData_WhenQueried()
    {
        // Arrange
        var handler = new GetPermissionsQueryHandler();

        // Act
        Result<List<PermissionResponse>> result = await handler.Handle(new GetPermissionsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(p => p.Code).ShouldBe(Permission.All.Select(p => p.Code));

        PermissionResponse assignRoles = result.Value.Single(p => p.Code == PermissionCodes.Users.AssignRoles);
        assignRoles.Name.ShouldBe("Alterar funções de colaboradores");
        assignRoles.Group.ShouldBe("Usuários");
        assignRoles.IsAdministrative.ShouldBeTrue();
    }
}
