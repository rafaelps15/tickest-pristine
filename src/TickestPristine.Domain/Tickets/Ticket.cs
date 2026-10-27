using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed class Ticket : Entity, IAuditable
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public Guid SectorId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Data da ação mais recente registrada no histórico do chamado; na abertura, a própria data de abertura. Nunca
    /// volta para uma data anterior.
    /// </summary>
    public DateTime LastActivityAtUtc { get; set; }

    public DateTime DueAtUtc { get; set; }
    public DateTime SlaWarningAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public TicketSlaPlan SlaPlan { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}
