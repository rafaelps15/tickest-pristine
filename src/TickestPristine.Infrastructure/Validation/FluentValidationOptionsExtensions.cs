using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TickestPristine.Infrastructure.Validation;

internal static class FluentValidationOptionsExtensions
{
    /// <summary>
    /// Valida as opções com o <see cref="IValidator{T}"/> registrado para elas; junto com <c>ValidateOnStart</c>, a
    /// configuração inválida impede a subida.
    /// </summary>
    public static OptionsBuilder<TOptions> ValidateFluently<TOptions>(this OptionsBuilder<TOptions> builder)
        where TOptions : class
    {
        builder.Services.AddSingleton<IValidateOptions<TOptions>>(serviceProvider =>
            new FluentValidationOptions<TOptions>(builder.Name, serviceProvider.GetRequiredService<IValidator<TOptions>>()));

        return builder;
    }
}
