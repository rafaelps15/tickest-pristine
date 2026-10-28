namespace TickestPristine.Application.Tickets.GetOverview;

/// <summary>
/// Cada contador é igual ao <c>totalCount</c> de <c>GET tickets</c> com o mesmo recorte e o filtro correspondente
/// (spec 026, REQ-3). <see cref="ServerNowUtc"/> é o instante usado para a situação de SLA.
/// </summary>
public sealed class TicketOverviewResponse
{
    public DateTime ServerNowUtc { get; set; }
    public TicketOverviewVolumeResponse Volume { get; set; } = new();
    public TicketOverviewSlaResponse Sla { get; set; } = new();
    public TicketOverviewAttentionResponse Attention { get; set; } = new();
}

/// <summary>
/// Chamados por status; <see cref="BacklogCount"/> soma Aberto e Em andamento.
/// </summary>
public sealed class TicketOverviewVolumeResponse
{
    public int TotalCount { get; set; }
    public int OpenCount { get; set; }
    public int InProgressCount { get; set; }
    public int ResolvedCount { get; set; }
    public int ClosedCount { get; set; }
    public int CanceledCount { get; set; }
    public int BacklogCount { get; set; }
}

/// <summary>
/// Chamados por situação de SLA, as mesmas de <c>slaStatus</c> na listagem.
/// </summary>
public sealed class TicketOverviewSlaResponse
{
    public int OnTimeCount { get; set; }
    public int AtRiskCount { get; set; }
    public int BreachedCount { get; set; }
    public int MetCount { get; set; }
    public int MissedCount { get; set; }
    public int NotApplicableCount { get; set; }
}

/// <summary>
/// Pontos de atenção: atrasados (a situação de SLA "atrasado"), críticos (Urgentes ainda ativos) e ativos sem
/// responsável.
/// </summary>
public sealed class TicketOverviewAttentionResponse
{
    public int BreachedCount { get; set; }
    public int CriticalCount { get; set; }
    public int UnassignedCount { get; set; }
}
