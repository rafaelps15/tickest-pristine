using System.Reflection;
using TickestPristine.Domain.Roles;

namespace TickestPristine.Application.UnitTests.Authorization;

public sealed class PermissionTests
{
    [Fact]
    public void All_Should_HaveUniqueCodes()
    {
        // Arrange
        IReadOnlyList<Permission> catalog = Permission.All;

        // Act
        var codes = catalog.Select(p => p.Code).ToList();

        // Assert
        codes.ShouldBeUnique();
    }

    [Fact]
    public void All_Should_ContainEveryPermissionCodeConstant()
    {
        // Arrange
        var constants = typeof(PermissionCodes)
            .GetNestedTypes()
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        // Act
        var codes = Permission.All.Select(p => p.Code).ToList();

        // Assert
        codes.ShouldBe(constants, ignoreOrder: true);
    }

    [Theory]
    [InlineData(PermissionCodes.Roles.Manage, true)]
    [InlineData(PermissionCodes.Tickets.Create, false)]
    public void Find_Should_ReturnAdministrativeFlagFromCatalog(string code, bool expected)
    {
        // Arrange
        Permission catalogPermission = Permission.All.Single(p => p.Code == code);

        // Act
        var permission = Permission.Find(code);

        // Assert
        permission.ShouldNotBeNull();
        permission.ShouldBeSameAs(catalogPermission);
        permission.IsAdministrative.ShouldBe(expected);
    }

    [Fact]
    public void GetDisplayName_Should_ReturnCode_WhenCodeIsNotInCatalog()
    {
        // Arrange
        const string unknownCode = "legacy:unknown";

        // Act
        string displayName = Permission.GetDisplayName(unknownCode);

        // Assert
        displayName.ShouldBe(unknownCode);
    }
}
