using TickestPristine.Application.Permissions;
using TickestPristine.Application.Roles.AssignPermissions;
using TickestPristine.Application.Roles.Create;
using FluentValidation.TestHelper;

namespace TickestPristine.Application.UnitTests.Roles;

public sealed class RoleValidatorsTests
{
    private readonly AssignRolePermissionsCommandValidator _assignPermissionsValidator = new();
    private readonly CreateRoleCommandValidator _createValidator = new();

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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateValidator_Should_HaveError_WhenNameIsEmptyOrOnlySpaces(string name)
    {
        // Arrange
        var command = new CreateRoleCommand { RoleName = name };

        // Act
        TestValidationResult<CreateRoleCommand> result = _createValidator.TestValidate(command);

        // Assert
        // O rótulo "Função" vem do resolvedor configurado na aplicação; é conferido no teste de integração.
        result.ShouldHaveValidationErrorFor(c => c.RoleName);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenNameHasMoreThan100CharactersAfterTrimming()
    {
        // Arrange
        var command = new CreateRoleCommand { RoleName = new string('a', 101) };

        // Act
        TestValidationResult<CreateRoleCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.RoleName)
            .When(failure => failure.ErrorMessage.EndsWith("deve ter no máximo 100 caracteres.", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateValidator_Should_NotHaveErrors_WhenNameHas100CharactersBetweenSpaces()
    {
        // Arrange: as pontas não contam no limite
        var command = new CreateRoleCommand { RoleName = $"  {new string('a', 100)}  " };

        // Act
        TestValidationResult<CreateRoleCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenNameIsAGuid()
    {
        // Arrange: na transição, a rota antiga pelo nome se confundiria com a rota pelo id (DEC-11)
        var command = new CreateRoleCommand { RoleName = $" {Guid.NewGuid()} " };

        // Act
        TestValidationResult<CreateRoleCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.RoleName)
            .When(failure => failure.ErrorMessage.EndsWith("não pode ter o formato de um identificador (Guid).", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateValidator_Should_NotHaveErrors_WhenNameIsValid()
    {
        // Arrange
        var command = new CreateRoleCommand { RoleName = "Supervisor" };

        // Act
        TestValidationResult<CreateRoleCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
