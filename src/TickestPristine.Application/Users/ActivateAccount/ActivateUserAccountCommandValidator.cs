using FluentValidation;

namespace TickestPristine.Application.Users.ActivateAccount;

internal sealed class ActivateUserAccountCommandValidator : AbstractValidator<ActivateUserAccountCommand>
{
    /// <summary>
    /// Tamanho máximo aceito para o token; o gerado tem 43 caracteres (32 bytes em Base64Url).
    /// </summary>
    internal const int TokenMaxLength = 200;

    private const string StrengthRequirementsMessage =
        "A senha precisa ter pelo menos 6 caracteres, incluindo uma letra maiúscula e um caractere especial (como !, @, # ou $).";

    public ActivateUserAccountCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty().MaximumLength(TokenMaxLength);
        RuleFor(c => c.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe a nova senha.")
            .MinimumLength(6).WithMessage(StrengthRequirementsMessage)
            .Matches(@"\p{Lu}").WithMessage(StrengthRequirementsMessage)
            .Matches(@"[^\p{L}\p{N}\s]").WithMessage(StrengthRequirementsMessage);
    }
}
