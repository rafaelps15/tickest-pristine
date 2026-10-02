using FluentValidation;

namespace TickestPristine.Application.Users;

/// <summary>
/// Regras de validação compartilhadas pelos casos de uso de usuário.
/// </summary>
internal static class UserValidationRules
{
    public const int NameMaxLength = 100;

    /// <summary>
    /// Tamanho máximo de um endereço de e-mail válido (RFC 5321).
    /// </summary>
    public const int EmailMaxLength = 254;

    private const string StrengthRequirementsMessage =
        "A senha precisa ter pelo menos 6 caracteres, incluindo uma letra maiúscula e um caractere especial (como !, @, # ou $).";

    /// <summary>
    /// Política de senha forte: pelo menos 6 caracteres, uma letra maiúscula e um caractere especial.
    /// </summary>
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .MinimumLength(6).WithMessage(StrengthRequirementsMessage)
            .Matches(@"\p{Lu}").WithMessage(StrengthRequirementsMessage)
            .Matches(@"[^\p{L}\p{N}\s]").WithMessage(StrengthRequirementsMessage);
}
