using FluentValidation;

namespace TickestPristine.Application.Departments.Delete;

internal sealed class DeleteDepartmentCommandValidator : AbstractValidator<DeleteDepartmentCommand>
{
    public DeleteDepartmentCommandValidator()
    {
        RuleFor(c => c.DepartmentId).NotEmpty();
    }
}
