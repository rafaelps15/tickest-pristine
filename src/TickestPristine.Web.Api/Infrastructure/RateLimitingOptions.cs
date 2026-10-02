using Microsoft.Extensions.Options;

namespace TickestPristine.Web.Api.Infrastructure;

/// <summary>
/// Configuração da seção "RateLimiting": limites por janela fixa, geral e dos endpoints de autenticação.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [ValidateObjectMembers]
    public FixedWindowLimitOptions Global { get; init; } = new();

    [ValidateObjectMembers]
    public FixedWindowLimitOptions Authentication { get; init; } = new();
}
