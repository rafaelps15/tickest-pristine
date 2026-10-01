using TickestPristine.Application.Authorization;
using TickestPristine.Application.Roles.AssignPermissions;
using TickestPristine.Application.Roles.Create;
using FluentValidation.TestHelper;

namespace TickestPristine.Application.UnitTests.Roles;

public sealed class RoleValidatorsTests
{
    private readonly CreateRoleCommandValidator _createValidator = new();
    private readonly AssignRolePermissionsCommandValidator _assignPermissionsValidator = new();

    [Fact]
    public void CreateValidator_Should_HaveError_WhenNameIsEmpty()
    {
        // Arrange
        var command = new CreateRoleCommand { Name = string.Empty };

        // Act
        TestValidationResult<CreateRoleCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void CreateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new CreateRoleCommand { Name = "Manager" };

        // Act
        TestValidationResult<CreateRoleCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AssignPermissionsValidator_Should_HaveError_WhenPermissionCodeIsUnknown()
    {
        // Arrange
        var command = new AssignRolePermissionsCommand
        {
            RoleId = Guid.NewGuid(),
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
            RoleId = Guid.NewGuid(),
            PermissionCodes = [PermissionCodes.Tickets.Create]
        };

        // Act
        TestValidationResult<AssignRolePermissionsCommand> result = _assignPermissionsValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AssignPermissionsValidator_Should_HaveError_WhenRoleIdIsEmpty()
    {
        // Arrange
        var command = new AssignRolePermissionsCommand
        {
            RoleId = Guid.Empty,
            PermissionCodes = [PermissionCodes.Tickets.Create]
        };

        // Act
        TestValidationResult<AssignRolePermissionsCommand> result = _assignPermissionsValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.RoleId);
    }

    [Fact]
    public void AssignPermissionsValidator_Should_HaveError_WhenPermissionCodesIsNull()
    {
        // Arrange
        var command = new AssignRolePermissionsCommand
        {
            RoleId = Guid.NewGuid(),
            PermissionCodes = null!
        };

        // Act
        TestValidationResult<AssignRolePermissionsCommand> result = _assignPermissionsValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.PermissionCodes);
    }
}
