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
        var command = new CreateRoleCommand { Name = string.Empty };

        TestValidationResult<CreateRoleCommand> result = _createValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void CreateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new CreateRoleCommand { Name = "Manager" };

        TestValidationResult<CreateRoleCommand> result = _createValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AssignPermissionsValidator_Should_HaveError_WhenPermissionCodeIsUnknown()
    {
        var command = new AssignRolePermissionsCommand
        {
            RoleId = Guid.NewGuid(),
            PermissionCodes = ["not-a-real-permission"]
        };

        TestValidationResult<AssignRolePermissionsCommand> result = _assignPermissionsValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.PermissionCodes);
    }

    [Fact]
    public void AssignPermissionsValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new AssignRolePermissionsCommand
        {
            RoleId = Guid.NewGuid(),
            PermissionCodes = [PermissionCodes.Tickets.Create]
        };

        TestValidationResult<AssignRolePermissionsCommand> result = _assignPermissionsValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
