using Microsoft.Extensions.Options;

namespace TickestPristine.Web.Api.Infrastructure;

/// <summary>
/// Validador gerado pelo .NET a partir dos atributos de <see cref="RateLimitingOptions"/>, incluindo os limites aninhados.
/// </summary>
[OptionsValidator]
internal sealed partial class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>;
