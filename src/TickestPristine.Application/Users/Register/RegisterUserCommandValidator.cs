using FluentValidation;

namespace TickestPristine.Application.Users.Register;

internal sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    private const string StrengthRequirementsMessage =
        "A senha precisa ter pelo menos 6 caracteres, incluindo uma letra maiúscula e um caractere especial (como !, @, # ou $).";

    public RegisterUserCommandValidator()
    {
        RuleFor(c => c.FirstName).NotEmpty();
        RuleFor(c => c.LastName).NotEmpty();
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe a senha.")
            .MinimumLength(6).WithMessage(StrengthRequirementsMessage)
            .Matches(@"\p{Lu}").WithMessage(StrengthRequirementsMessage)
            .Matches(@"[^\p{L}\p{N}\s]").WithMessage(StrengthRequirementsMessage);
    }
}
