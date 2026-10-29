using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets.GetAll;

public sealed class TicketSummaryResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    public Guid RequesterId { get; set; }
    public string RequesterName { get; set; }
    public string RequesterEmail { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public Guid SectorId { get; set; }
    public string SectorName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime DueAtUtc { get; set; }
    public DateTime SlaWarningAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public TicketSlaStatus SlaStatus { get; set; }

    /// <summary>
    /// Quem o chamado ativo está aguardando; nulo enquanto o chamado está inativo (spec 029).
    /// </summary>
    public TicketWaitingOn? WaitingOn { get; set; }
}
