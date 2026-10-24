namespace TickestPristine.Web.Api.Infrastructure;

/// <summary>
/// Configuração da seção "Cors": origens (front-ends) autorizadas a chamar a API pelo navegador.
/// </summary>
public sealed class CorsPolicyOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; init; } = [];

    /// <summary>
    /// Aceita também qualquer origem local (localhost, 127.0.0.1), em qualquer porta. Só para desenvolvimento, em que o
    /// servidor do front pode subir numa porta diferente a cada execução.
    /// </summary>
    public bool AllowLoopbackOrigins { get; init; }
}
