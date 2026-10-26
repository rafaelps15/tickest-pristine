using System.ComponentModel.DataAnnotations;

namespace TickestPristine.Infrastructure.Tickets.SlaMonitoring;

/// <summary>
/// Configuração do monitoramento do SLA (seção "SlaMonitor"). Valores menores que 1 impedem a API de subir.
/// </summary>
public sealed class SlaMonitorSettings
{
    public const string SettingsKey = "SlaMonitor";

    /// <summary>
    /// Se o monitoramento roda. Os testes de integração o desligam.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Quantidade máxima de cronômetros por lote.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int BatchSize { get; init; } = 200;

    /// <summary>
    /// Espera entre um lote e o seguinte, em milissegundos.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int SleepTimeInMilliseconds { get; init; } = 60_000;
}
