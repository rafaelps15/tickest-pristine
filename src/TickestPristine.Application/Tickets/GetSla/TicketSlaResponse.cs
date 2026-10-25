namespace TickestPristine.Application.Tickets.GetSla;

/// <summary>
/// SLA do chamado calculado no servidor em <see cref="ServerNowUtc"/>: o plano aplicado e todos os ciclos de cada
/// objetivo, em ordem de objetivo e ciclo.
/// </summary>
public sealed class TicketSlaResponse
{
    public DateTime ServerNowUtc { get; set; }
    public TicketSlaPlanResponse Plan { get; set; }
    public List<TicketSlaObjectiveResponse> Objectives { get; set; } = [];
}
