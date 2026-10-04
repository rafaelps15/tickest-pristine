using FluentValidation;

namespace TickestPristine.Application.Users.ChangeRequiredPassword;

internal sealed class ChangeRequiredUserPasswordCommandValidator : AbstractValidator<ChangeRequiredUserPasswordCommand>
{
    public ChangeRequiredUserPasswordCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.CurrentPassword).NotEmpty().WithMessage("Informe a senha atual.");
        RuleFor(c => c.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe a nova senha.")
            .StrongPassword()
            .NotEqual(c => c.CurrentPassword).WithMessage("A nova senha precisa ser diferente da atual.");
    }
}
