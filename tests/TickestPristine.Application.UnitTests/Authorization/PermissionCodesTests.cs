using TickestPristine.Application.Authorization;

namespace TickestPristine.Application.UnitTests.Authorization;

public sealed class PermissionCodesTests
{
    [Fact]
    public void Definitions_Should_HaveUniqueCodes()
    {
        // Arrange
        IReadOnlyList<PermissionDefinition> definitions = PermissionCodes.Definitions;

        // Act
        var codes = definitions.Select(d => d.Code).ToList();

        // Assert
        codes.ShouldBeUnique();
    }

    [Fact]
    public void Definitions_Should_HaveNameDescriptionAndGroupForEveryPermission()
    {
        // Arrange
        IReadOnlyList<PermissionDefinition> definitions = PermissionCodes.Definitions;

        // Act
        var incomplete = definitions
            .Where(d => string.IsNullOrWhiteSpace(d.Name) ||
                        string.IsNullOrWhiteSpace(d.Description) ||
                        string.IsNullOrWhiteSpace(d.Group))
            .ToList();

        // Assert
        incomplete.ShouldBeEmpty();
    }

    [Fact]
    public void PermissionDefinition_Should_BeAdministrativeByDefault()
    {
        // Arrange
        const string code = "new:permission";

        // Act
        var definition = new PermissionDefinition(code, "Nova", "Descrição", "Grupo");

        // Assert
        definition.IsAdministrative.ShouldBeTrue();
    }

    [Fact]
    public void Administrative_Should_ContainExactlyTheDefinitionsMarkedAsAdministrative()
    {
        // Arrange
        IReadOnlyList<PermissionDefinition> definitions = PermissionCodes.Definitions;

        // Act
        var expected = definitions.Where(d => d.IsAdministrative).Select(d => d.Code).ToList();

        // Assert
        PermissionCodes.Administrative.ShouldBe(expected, ignoreOrder: true);
    }

    [Theory]
    [InlineData(PermissionCodes.Users.AssignRoles)]
    [InlineData(PermissionCodes.Users.Update)]
    [InlineData(PermissionCodes.Roles.Manage)]
    [InlineData(PermissionCodes.Departments.Manage)]
    public void Administrative_Should_ContainPermissionsThatGivePowerOverOthers(string permission)
    {
        // Arrange
        IReadOnlyList<string> administrative = PermissionCodes.Administrative;

        // Act
        bool isAdministrative = administrative.Contains(permission);

        // Assert
        isAdministrative.ShouldBeTrue();
    }

    [Theory]
    [InlineData(PermissionCodes.Tickets.Create)]
    [InlineData(PermissionCodes.Tickets.Manage)]
    public void Administrative_Should_NotContainTicketPermissions(string permission)
    {
        // Arrange
        IReadOnlyList<string> administrative = PermissionCodes.Administrative;

        // Act
        bool isAdministrative = administrative.Contains(permission);

        // Assert
        isAdministrative.ShouldBeFalse();
    }

    [Fact]
    public void GetDisplayName_Should_ReturnDisplayName_WhenCodeIsInCatalog()
    {
        // Arrange
        const string code = PermissionCodes.Users.AssignRoles;

        // Act
        string name = PermissionCodes.GetDisplayName(code);

        // Assert
        name.ShouldBe("Alterar funções de colaboradores");
    }

    [Fact]
    public void GetDisplayName_Should_ReturnCode_WhenCodeIsNotInCatalog()
    {
        // Arrange
        const string code = "legacy:unknown";

        // Act
        string name = PermissionCodes.GetDisplayName(code);

        // Assert
        name.ShouldBe(code);
    }
}
