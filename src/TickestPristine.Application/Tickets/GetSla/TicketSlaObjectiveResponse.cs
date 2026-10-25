using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets.GetSla;

/// <summary>
/// Um ciclo de um objetivo do SLA. Os minutos são úteis, no calendário do plano: o consumido é truncado e o restante é
/// a meta menos o consumido, nunca negativo. Sem cronômetro para o objetivo, o item vem com
/// <see cref="SlaObjectiveState.NotApplicable"/> e datas e minutos nulos.
/// </summary>
public sealed class TicketSlaObjectiveResponse
{
    public SlaMetric Metric { get; set; }
    public int Cycle { get; set; }
    public SlaObjectiveState State { get; set; }
    public int? TargetMinutes { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? WarningAtUtc { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? StoppedAtUtc { get; set; }
    public int? ConsumedMinutes { get; set; }
    public int? RemainingMinutes { get; set; }
    public bool IsCurrentCycle { get; set; }
}
