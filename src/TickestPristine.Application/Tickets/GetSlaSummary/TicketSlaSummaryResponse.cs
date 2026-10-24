namespace TickestPristine.Application.Tickets.GetSlaSummary;

/// <summary>
/// Os contadores somam só os chamados ativos com o relógio do SLA correndo; os reabertos depois de uma resolução
/// entram em <see cref="ActiveCount"/>, mas não nos contadores, porque o SLA deles já foi decidido. O
/// <see cref="ServerNowUtc"/> deixa o cliente recalcular os badges pelo relógio do servidor.
/// </summary>
public sealed class TicketSlaSummaryResponse
{
    public int ActiveCount { get; set; }
    public int OnTimeCount { get; set; }
    public int AtRiskCount { get; set; }
    public int BreachedCount { get; set; }
    public DateTime ServerNowUtc { get; set; }
}
