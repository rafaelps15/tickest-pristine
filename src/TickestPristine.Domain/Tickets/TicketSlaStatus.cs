namespace TickestPristine.Domain.Tickets;

/// <summary>
/// Situação do chamado em relação ao prazo de resolução. Os três primeiros valem para chamados ainda não resolvidos;
/// <see cref="Met"/> e <see cref="Missed"/> ficam congelados na primeira resolução, mesmo que o chamado seja reaberto.
/// </summary>
public enum TicketSlaStatus
{
    OnTime = 1,
    AtRisk = 2,
    Breached = 3,
    Met = 4,
    Missed = 5,
    NotApplicable = 6
}
