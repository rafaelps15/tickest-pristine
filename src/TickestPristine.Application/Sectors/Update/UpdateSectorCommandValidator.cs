using FluentValidation;

namespace TickestPristine.Application.Sectors.Update;

internal sealed class UpdateSectorCommandValidator : AbstractValidator<UpdateSectorCommand>
{
    public UpdateSectorCommandValidator()
    {
        RuleFor(c => c.SectorId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(SectorValidationRules.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(SectorValidationRules.DescriptionMaxLength);
    }
}
