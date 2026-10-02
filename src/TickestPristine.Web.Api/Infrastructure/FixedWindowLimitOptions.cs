using System.ComponentModel.DataAnnotations;

namespace TickestPristine.Web.Api.Infrastructure;

/// <summary>
/// Quantidade de requisições permitidas por janela de tempo.
/// </summary>
public sealed class FixedWindowLimitOptions
{
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; init; }

    [Range(1, int.MaxValue)]
    public int WindowInSeconds { get; init; }
}
