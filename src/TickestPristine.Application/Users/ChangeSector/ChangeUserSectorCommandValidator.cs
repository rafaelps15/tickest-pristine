using FluentValidation;

namespace TickestPristine.Application.Users.ChangeSector;

internal sealed class ChangeUserSectorCommandValidator : AbstractValidator<ChangeUserSectorCommand>
{
    public ChangeUserSectorCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.SectorId).NotEqual(Guid.Empty);
    }
}
