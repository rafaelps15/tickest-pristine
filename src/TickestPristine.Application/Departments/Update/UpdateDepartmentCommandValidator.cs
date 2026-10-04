using FluentValidation;

namespace TickestPristine.Application.Departments.Update;

internal sealed class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(c => c.DepartmentId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(DepartmentValidationRules.NameMaxLength);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(DepartmentValidationRules.DescriptionMaxLength);
    }
}
