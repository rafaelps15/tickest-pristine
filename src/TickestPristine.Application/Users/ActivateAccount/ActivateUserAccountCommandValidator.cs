using FluentValidation;

namespace TickestPristine.Application.Users.ActivateAccount;

internal sealed class ActivateUserAccountCommandValidator : AbstractValidator<ActivateUserAccountCommand>
{
    /// <summary>
    /// Tamanho máximo aceito para o token; o gerado tem 43 caracteres (32 bytes em Base64Url).
    /// </summary>
    internal const int TokenMaxLength = 200;

    public ActivateUserAccountCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty().MaximumLength(TokenMaxLength);
        RuleFor(c => c.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe a nova senha.")
            .StrongPassword();
    }
}
