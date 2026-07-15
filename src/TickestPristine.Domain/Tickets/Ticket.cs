using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed class Ticket : Entity
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    public Guid OpenedByUserId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid SectorId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}
