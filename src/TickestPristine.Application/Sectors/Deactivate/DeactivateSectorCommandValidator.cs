using FluentValidation;

namespace TickestPristine.Application.Sectors.Deactivate;

internal sealed class DeactivateSectorCommandValidator : AbstractValidator<DeactivateSectorCommand>
{
    public DeactivateSectorCommandValidator()
    {
        RuleFor(c => c.SectorId).NotEmpty();
    }
}
