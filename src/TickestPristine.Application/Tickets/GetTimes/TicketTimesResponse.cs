namespace TickestPristine.Application.Tickets.GetTimes;

/// <summary>
/// Abaixo de <see cref="MinimumSampleSize"/> chamados na amostra, média e mediana vêm nulas (dados insuficientes); a
/// amostra vem sempre.
/// </summary>
public sealed class TicketTimesResponse
{
    public int MinimumSampleSize { get; set; }
    public TicketTimeStatisticsResponse FirstResponse { get; set; } = new();
    public TicketTimeStatisticsResponse Resolution { get; set; } = new();
}

/// <summary>
/// Média e mediana em minutos úteis, pelo calendário do plano de SLA de cada chamado.
/// </summary>
public sealed class TicketTimeStatisticsResponse
{
    public double? AverageMinutes { get; set; }
    public double? MedianMinutes { get; set; }
    public int SampleSize { get; set; }
}
