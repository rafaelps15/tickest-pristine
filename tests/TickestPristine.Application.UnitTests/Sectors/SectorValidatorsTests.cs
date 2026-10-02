using TickestPristine.Application.Sectors.Create;
using TickestPristine.Application.Sectors.Deactivate;
using TickestPristine.Application.Sectors.Update;
using FluentValidation.TestHelper;

namespace TickestPristine.Application.UnitTests.Sectors;

public sealed class SectorValidatorsTests
{
    private readonly CreateSectorCommandValidator _createValidator = new();
    private readonly UpdateSectorCommandValidator _updateValidator = new();
    private readonly DeactivateSectorCommandValidator _deactivateValidator = new();

    [Fact]
    public void CreateValidator_Should_HaveError_WhenNameIsEmpty()
    {
        // Arrange
        var command = new CreateSectorCommand { Name = string.Empty, DepartmentId = Guid.NewGuid() };

        // Act
        TestValidationResult<CreateSectorCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDepartmentIdIsEmpty()
    {
        // Arrange
        var command = new CreateSectorCommand { Name = "Helpdesk", DepartmentId = Guid.Empty };

        // Act
        TestValidationResult<CreateSectorCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.DepartmentId);
    }

    [Fact]
    public void CreateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new CreateSectorCommand { Name = "Helpdesk", DepartmentId = Guid.NewGuid() };

        // Act
        TestValidationResult<CreateSectorCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenSectorIdIsEmpty()
    {
        // Arrange
        var command = new UpdateSectorCommand { SectorId = Guid.Empty, Name = "Helpdesk" };

        // Act
        TestValidationResult<UpdateSectorCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.SectorId);
    }

    [Fact]
    public void UpdateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new UpdateSectorCommand { SectorId = Guid.NewGuid(), Name = "Helpdesk" };

        // Act
        TestValidationResult<UpdateSectorCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeactivateValidator_Should_HaveError_WhenSectorIdIsEmpty()
    {
        // Arrange
        var command = new DeactivateSectorCommand(Guid.Empty);

        // Act
        TestValidationResult<DeactivateSectorCommand> result = _deactivateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.SectorId);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenNameExceedsMaxLength()
    {
        // Arrange
        var command = new CreateSectorCommand { Name = new string('a', 101), Description = null, DepartmentId = Guid.NewGuid() };

        // Act
        TestValidationResult<CreateSectorCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDescriptionExceedsMaxLength()
    {
        // Arrange
        var command = new CreateSectorCommand { Name = "Helpdesk", Description = new string('a', 501), DepartmentId = Guid.NewGuid() };

        // Act
        TestValidationResult<CreateSectorCommand> result = _createValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenNameIsEmpty()
    {
        // Arrange
        var command = new UpdateSectorCommand { SectorId = Guid.NewGuid(), Name = string.Empty, Description = null };

        // Act
        TestValidationResult<UpdateSectorCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenNameExceedsMaxLength()
    {
        // Arrange
        var command = new UpdateSectorCommand { SectorId = Guid.NewGuid(), Name = new string('a', 101), Description = null };

        // Act
        TestValidationResult<UpdateSectorCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenDescriptionExceedsMaxLength()
    {
        // Arrange
        var command = new UpdateSectorCommand { SectorId = Guid.NewGuid(), Name = "Helpdesk", Description = new string('a', 501) };

        // Act
        TestValidationResult<UpdateSectorCommand> result = _updateValidator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(c => c.Description);
    }

    [Fact]
    public void DeactivateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        // Arrange
        var command = new DeactivateSectorCommand(Guid.NewGuid());

        // Act
        TestValidationResult<DeactivateSectorCommand> result = _deactivateValidator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
