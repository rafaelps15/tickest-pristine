using FluentValidation;

namespace TickestPristine.Application.Sectors.Create;

internal sealed class CreateSectorCommandValidator : AbstractValidator<CreateSectorCommand>
{
    public CreateSectorCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Description).MaximumLength(500);
        RuleFor(c => c.DepartmentId).NotEmpty();
    }
}
