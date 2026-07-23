using TickestPristine.Application.Departments.Create;
using TickestPristine.Application.Departments.Delete;
using TickestPristine.Application.Departments.Update;
using FluentValidation.TestHelper;

namespace TickestPristine.Application.UnitTests.Departments;

public sealed class DepartmentValidatorsTests
{
    private readonly CreateDepartmentCommandValidator _createValidator = new();
    private readonly UpdateDepartmentCommandValidator _updateValidator = new();
    private readonly DeleteDepartmentCommandValidator _deleteValidator = new();

    [Fact]
    public void CreateValidator_Should_HaveError_WhenNameIsEmpty()
    {
        var command = new CreateDepartmentCommand { Name = string.Empty, Description = "Some description" };

        TestValidationResult<CreateDepartmentCommand> result = _createValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.Name);
    }

    [Fact]
    public void CreateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new CreateDepartmentCommand { Name = "Support", Description = "Customer support department" };

        TestValidationResult<CreateDepartmentCommand> result = _createValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateValidator_Should_HaveError_WhenDepartmentIdIsEmpty()
    {
        var command = new UpdateDepartmentCommand { DepartmentId = Guid.Empty, Name = "Support", Description = "Updated" };

        TestValidationResult<UpdateDepartmentCommand> result = _updateValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.DepartmentId);
    }

    [Fact]
    public void UpdateValidator_Should_NotHaveErrors_WhenCommandIsValid()
    {
        var command = new UpdateDepartmentCommand { DepartmentId = Guid.NewGuid(), Name = "Support", Description = "Updated" };

        TestValidationResult<UpdateDepartmentCommand> result = _updateValidator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DeleteValidator_Should_HaveError_WhenDepartmentIdIsEmpty()
    {
        var command = new DeleteDepartmentCommand(Guid.Empty);

        TestValidationResult<DeleteDepartmentCommand> result = _deleteValidator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.DepartmentId);
    }
}
