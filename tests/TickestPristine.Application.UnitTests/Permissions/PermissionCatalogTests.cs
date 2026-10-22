using System.Reflection;
using TickestPristine.Application.Permissions;

namespace TickestPristine.Application.UnitTests.Permissions;

public sealed class PermissionCatalogTests
{
    [Fact]
    public void All_Should_HaveUniqueCodes()
    {
        // Arrange
        IReadOnlyList<PermissionCatalogEntry> catalog = PermissionCatalog.All;

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
        var codes = PermissionCatalog.All.Select(p => p.Code).ToList();

        // Assert
        codes.ShouldBe(constants, ignoreOrder: true);
    }

    [Theory]
    [InlineData(PermissionCodes.Roles.Manage, true)]
    [InlineData(PermissionCodes.Tickets.Create, false)]
    public void Find_Should_ReturnAdministrativeFlagFromCatalog(string code, bool expected)
    {
        // Arrange
        PermissionCatalogEntry catalogPermission = PermissionCatalog.All.Single(p => p.Code == code);

        // Act
        PermissionCatalogEntry? permission = PermissionCatalog.Find(code);

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
        string displayName = PermissionCatalog.GetDisplayName(unknownCode);

        // Assert
        displayName.ShouldBe(unknownCode);
    }
}
