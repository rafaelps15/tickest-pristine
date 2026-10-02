using FluentValidation;

namespace TickestPristine.Application.Users.Deactivate;

internal sealed class DeactivateUserCommandValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
