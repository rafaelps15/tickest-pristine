using TickestPristine.Application.Departments.Create;
using TickestPristine.Application.Departments.Deactivate;
using TickestPristine.Application.Departments.Update;
using FluentValidation.TestHelper;

namespace TickestPristine.Application.UnitTests.Departments;

public sealed class DepartmentValidatorsTests
{
    private readonly CreateDepartmentCommandValidator _createValidator = new();
    private readonly UpdateDepartmentCommandValidator _updateValidator = new();
    private readonly DeactivateDepartmentCommandValidator _deactivateValidator = new();

    [Fact]
    public void CreateValidator_Should_HaveError_WhenNameIsEmpty()
    {
        // Arrange
        var command = new CreateDepartmentCommand { Name = string.Empty, Description = "Some description" };

        // Act
        TestValidationResult<CreateDepartmentCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void CreateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new CreateDepartmentCommand { Name = "Support", Description = "Customer support department" };

        // Act
        TestValidationResult<CreateDepartmentCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenDepartmentIdIsEmpty()
    {
        // Arrange
        var command = new UpdateDepartmentCommand { DepartmentId = Guid.Empty, Name = "Support", Description = "Updated" };

        // Act
        TestValidationResult<UpdateDepartmentCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.DepartmentId);
    }

    [Fact]
    public void UpdateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new UpdateDepartmentCommand { DepartmentId = Guid.NewGuid(), Name = "Support", Description = "Updated" };

        // Act
        TestValidationResult<UpdateDepartmentCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeactivateValidator_Should_HaveError_WhenDepartmentIdIsEmpty()
    {
        // Arrange
        var command = new DeactivateDepartmentCommand(Guid.Empty);

        // Act
        TestValidationResult<DeactivateDepartmentCommand> result = _deactivateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.DepartmentId);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenNameExceedsMaxLength()
    {
        // Arrange
        var command = new CreateDepartmentCommand { Name = new string('a', 101), Description = "Customer support department" };

        // Act
        TestValidationResult<CreateDepartmentCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDescriptionIsEmpty()
    {
        // Arrange
        var command = new CreateDepartmentCommand { Name = "Support", Description = string.Empty };

        // Act
        TestValidationResult<CreateDepartmentCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDescriptionExceedsMaxLength()
    {
        // Arrange
        var command = new CreateDepartmentCommand { Name = "Support", Description = new string('a', 501) };

        // Act
        TestValidationResult<CreateDepartmentCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenNameIsEmpty()
    {
        // Arrange
        var command = new UpdateDepartmentCommand { DepartmentId = Guid.NewGuid(), Name = string.Empty, Description = "Updated" };

        // Act
        TestValidationResult<UpdateDepartmentCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenNameExceedsMaxLength()
    {
        // Arrange
        var command = new UpdateDepartmentCommand { DepartmentId = Guid.NewGuid(), Name = new string('a', 101), Description = "Updated" };

        // Act
        TestValidationResult<UpdateDepartmentCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenDescriptionIsEmpty()
    {
        // Arrange
        var command = new UpdateDepartmentCommand { DepartmentId = Guid.NewGuid(), Name = "Support", Description = string.Empty };

        // Act
        TestValidationResult<UpdateDepartmentCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenDescriptionExceedsMaxLength()
    {
        // Arrange
        var command = new UpdateDepartmentCommand { DepartmentId = Guid.NewGuid(), Name = "Support", Description = new string('a', 501) };

        // Act
        TestValidationResult<UpdateDepartmentCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void DeactivateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new DeactivateDepartmentCommand(Guid.NewGuid());

        // Act
        TestValidationResult<DeactivateDepartmentCommand> result = _deactivateValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
