using FluentValidation;

namespace TickestPristine.Application.Users.ChangePassword;

internal sealed class ChangeUserPasswordCommandValidator : AbstractValidator<ChangeUserPasswordCommand>
{
    private const string StrengthRequirementsMessage =
        "A senha precisa ter pelo menos 6 caracteres, incluindo uma letra maiúscula e um caractere especial (como !, @, # ou $).";

    public ChangeUserPasswordCommandValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty().WithMessage("Informe a senha atual.");
        RuleFor(c => c.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe a nova senha.")
            .MinimumLength(6).WithMessage(StrengthRequirementsMessage)
            .Matches(@"\p{Lu}").WithMessage(StrengthRequirementsMessage)
            .Matches(@"[^\p{L}\p{N}\s]").WithMessage(StrengthRequirementsMessage)
            .NotEqual(c => c.CurrentPassword).WithMessage("A nova senha precisa ser diferente da atual.");
    }
}
