using TickestPristine.Application.Tickets.GetSla;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets.GetById;

public sealed class TicketResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    public Guid OpenedByUserId { get; set; }
    public string OpenedByName { get; set; }
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
    /// Se quem consulta pode cancelar o chamado agora (spec 016).
    /// </summary>
    public bool CanCancel { get; set; }

    /// <summary>
    /// Até quando o dono pode cancelar o próprio chamado: a abertura mais a janela da política de cancelamento.
    /// </summary>
    public DateTime OwnerCancelDeadlineUtc { get; set; }

    public TicketSlaResponse Sla { get; set; }
}
