namespace TickestPristine.Domain.Tickets;

/// <summary>
/// Estado de um cronômetro de SLA, calculado na leitura. <see cref="Running"/>, <see cref="AtRisk"/> e
/// <see cref="Breached"/> valem para cronômetros em aberto; <see cref="Met"/>, <see cref="Missed"/> e
/// <see cref="Stopped"/> são finais. <see cref="NotApplicable"/> indica que não há cronômetro para o objetivo.
/// </summary>
public enum SlaObjectiveState
{
    Running = 1,
    AtRisk = 2,
    Breached = 3,
    Met = 4,
    Missed = 5,
    Stopped = 6,
    NotApplicable = 7
}
