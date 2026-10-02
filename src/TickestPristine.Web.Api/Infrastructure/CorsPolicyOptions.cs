namespace TickestPristine.Web.Api.Infrastructure;

/// <summary>
/// Configuração da seção "Cors": origens (front-ends) autorizadas a chamar a API pelo navegador.
/// </summary>
public sealed class CorsPolicyOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; init; } = [];
}
