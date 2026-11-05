using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;

namespace TickestPristine.Infrastructure.Validation;

/// <summary>
/// Liga o validador do FluentValidation de uma opção de configuração à validação do Options: as mensagens de falha
/// viram a <see cref="OptionsValidationException"/>, que impede a subida quando o registro usa <c>ValidateOnStart</c>.
/// </summary>
internal sealed class FluentValidationOptions<TOptions>(string? optionsName, IValidator<TOptions> validator)
    : IValidateOptions<TOptions>
    where TOptions : class
{
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        // Uma opção registrada com outro nome fica para o validador dela.
        if (optionsName is not null && optionsName != name)
        {
            return ValidateOptionsResult.Skip;
        }

        ValidationResult result = validator.Validate(options);

        return result.IsValid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(result.Errors.Select(error => error.ErrorMessage));
    }
}
