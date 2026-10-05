using TickestPristine.Domain.Roles;
using TickestPristine.Application.Roles.AssignPermissions;
using FluentValidation.TestHelper;

namespace TickestPristine.Application.UnitTests.Roles;

public sealed class RoleValidatorsTests
{
    private readonly AssignRolePermissionsCommandValidator _assignPermissionsValidator = new();

    [Fact]
    public void AssignPermissionsValidator_Should_HaveError_WhenPermissionCodeIsUnknown()
    {
        // Arrange
        var command = new AssignRolePermissionsCommand
        {
            RoleName = "Atendente",
            PermissionCodes = ["not-a-real-permission"]
        };

        // Act
        TestValidationResult<AssignRolePermissionsCommand> result = _assignPermissionsValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("PermissionCodes[0]")
            .WithErrorMessage("'not-a-real-permission' não é um código de permissão conhecido.");
    }

    [Fact]
    public void AssignPermissionsValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new AssignRolePermissionsCommand
        {
            RoleName = "Atendente",
            PermissionCodes = [PermissionCodes.Tickets.Create]
        };

        // Act
        TestValidationResult<AssignRolePermissionsCommand> result = _assignPermissionsValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AssignPermissionsValidator_Should_HaveError_WhenRoleNameIsEmpty()
    {
        // Arrange
        var command = new AssignRolePermissionsCommand
        {
            RoleName = string.Empty,
            PermissionCodes = [PermissionCodes.Tickets.Create]
        };

        // Act
        TestValidationResult<AssignRolePermissionsCommand> result = _assignPermissionsValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.RoleName);
    }

    [Fact]
    public void AssignPermissionsValidator_Should_HaveError_WhenPermissionCodesIsNull()
    {
        // Arrange
        var command = new AssignRolePermissionsCommand
        {
            RoleName = "Atendente",
            PermissionCodes = null!
        };

        // Act
        TestValidationResult<AssignRolePermissionsCommand> result = _assignPermissionsValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.PermissionCodes);
    }
}
