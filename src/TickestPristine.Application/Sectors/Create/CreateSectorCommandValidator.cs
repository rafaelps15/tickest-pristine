using FluentValidation;

namespace TickestPristine.Application.Sectors.Create;

internal sealed class CreateSectorCommandValidator : AbstractValidator<CreateSectorCommand>
{
    public CreateSectorCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(SectorValidationRules.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(SectorValidationRules.DescriptionMaxLength);
        RuleFor(c => c.DepartmentId).NotEmpty();
    }
}
