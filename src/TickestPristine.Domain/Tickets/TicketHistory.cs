using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed class TicketHistory : Entity
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public TicketHistoryAction Action { get; set; }
    public string Description { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
