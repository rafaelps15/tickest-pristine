using FluentValidation;

namespace TickestPristine.Application.Users.Logout;

internal sealed class LogoutUserCommandValidator : AbstractValidator<LogoutUserCommand>
{
    public LogoutUserCommandValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty();
    }
}
