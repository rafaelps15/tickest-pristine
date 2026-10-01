using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TickestPristine.Application.Authorization;
using TickestPristine.Web.Api.Extensions;

namespace TickestPristine.IntegrationTests.Endpoints;

/// <summary>
/// Valida a checagem dos códigos de permissão feita no mapeamento dos endpoints.
/// </summary>
public sealed class HasPermissionTests
{
    [Fact]
    public void HasPermission_Should_Throw_WhenPermissionIsNotInCatalog()
    {
        // Arrange
        WebApplication app = WebApplication.CreateBuilder().Build();
        RouteHandlerBuilder route = app.MapGet("teste", () => Results.Ok());

        // Act
        Action act = () => route.HasPermission("inexistente:codigo");

        // Assert
        InvalidOperationException exception = act.ShouldThrow<InvalidOperationException>();
        exception.Message.ShouldContain("inexistente:codigo");
    }

    [Fact]
    public void HasPermission_Should_NotThrow_WhenPermissionIsInCatalog()
    {
        // Arrange
        WebApplication app = WebApplication.CreateBuilder().Build();
        RouteHandlerBuilder route = app.MapGet("teste", () => Results.Ok());

        // Act
        Action act = () => route.HasPermission(PermissionCodes.Users.Read);

        // Assert
        act.ShouldNotThrow();
    }
}
