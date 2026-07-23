using FluentValidation;

namespace TickestPristine.Application.Sectors.Delete;

internal sealed class DeleteSectorCommandValidator : AbstractValidator<DeleteSectorCommand>
{
    public DeleteSectorCommandValidator()
    {
        RuleFor(c => c.SectorId).NotEmpty();
    }
}
