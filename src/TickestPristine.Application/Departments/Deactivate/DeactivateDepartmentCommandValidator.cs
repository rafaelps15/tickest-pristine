using FluentValidation;

namespace TickestPristine.Application.Departments.Deactivate;

internal sealed class DeactivateDepartmentCommandValidator : AbstractValidator<DeactivateDepartmentCommand>
{
    public DeactivateDepartmentCommandValidator()
    {
        RuleFor(c => c.DepartmentId).NotEmpty();
    }
}
