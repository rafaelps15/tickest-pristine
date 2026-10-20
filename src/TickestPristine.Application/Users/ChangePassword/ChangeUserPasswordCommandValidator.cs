using FluentValidation;

namespace TickestPristine.Application.Users.ChangePassword;

internal sealed class ChangeUserPasswordCommandValidator : AbstractValidator<ChangeUserPasswordCommand>
{
    public ChangeUserPasswordCommandValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty().WithMessage("Informe a senha atual.");
        RuleFor(c => c.NewPassword).NewPassword(c => c.CurrentPassword);
    }
}
