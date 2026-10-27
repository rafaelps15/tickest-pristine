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

    /// <summary>
    /// Envio de anexos, por usuário (spec 011).
    /// </summary>
    [ValidateObjectMembers]
    public FixedWindowLimitOptions AttachmentUpload { get; init; } = new();

    /// <summary>
    /// Exclusão de anexos, por usuário (spec 011).
    /// </summary>
    [ValidateObjectMembers]
    public FixedWindowLimitOptions AttachmentDelete { get; init; } = new();
}
