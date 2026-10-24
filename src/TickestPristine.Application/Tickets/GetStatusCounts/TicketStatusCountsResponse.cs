namespace TickestPristine.Application.Tickets.GetStatusCounts;

/// <summary>
/// <see cref="TotalCount"/> soma todos os status, inclusive Fechado e Cancelado.
/// </summary>
public sealed class TicketStatusCountsResponse
{
    public int TotalCount { get; set; }
    public int OpenCount { get; set; }
    public int InProgressCount { get; set; }
    public int ResolvedCount { get; set; }
    public int ClosedCount { get; set; }
    public int CanceledCount { get; set; }
}
