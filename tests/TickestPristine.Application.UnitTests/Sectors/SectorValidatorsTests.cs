using TickestPristine.Application.Sectors.Create;
using TickestPristine.Application.Sectors.Delete;
using TickestPristine.Application.Sectors.Update;
using FluentValidation.TestHelper;

namespace TickestPristine.Application.UnitTests.Sectors;

public sealed class SectorValidatorsTests
{
    private readonly CreateSectorCommandValidator _createValidator = new();
    private readonly UpdateSectorCommandValidator _updateValidator = new();
    private readonly DeleteSectorCommandValidator _deleteValidator = new();

    [Fact]
    public void CreateValidator_Should_HaveError_WhenNameIsEmpty()
    {
        var command = new CreateSectorCommand { Name = string.Empty, DepartmentId = Guid.NewGuid() };

        TestValidationResult<CreateSectorCommand> result = _createValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void CreateValidator_Should_HaveError_WhenDepartmentIdIsEmpty()
    {
        var command = new CreateSectorCommand { Name = "Helpdesk", DepartmentId = Guid.Empty };

        TestValidationResult<CreateSectorCommand> result = _createValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.DepartmentId);
    }

    [Fact]
    public void CreateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new CreateSectorCommand { Name = "Helpdesk", DepartmentId = Guid.NewGuid() };

        TestValidationResult<CreateSectorCommand> result = _createValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenSectorIdIsEmpty()
    {
        var command = new UpdateSectorCommand { SectorId = Guid.Empty, Name = "Helpdesk" };

        TestValidationResult<UpdateSectorCommand> result = _updateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.SectorId);
    }

    [Fact]
    public void UpdateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new UpdateSectorCommand { SectorId = Guid.NewGuid(), Name = "Helpdesk" };

        TestValidationResult<UpdateSectorCommand> result = _updateValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeleteValidator_Should_HaveError_WhenSectorIdIsEmpty()
    {
        var command = new DeleteSectorCommand(Guid.Empty);

        TestValidationResult<DeleteSectorCommand> result = _deleteValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.SectorId);
    }
}
