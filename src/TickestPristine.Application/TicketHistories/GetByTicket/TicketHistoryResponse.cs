using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.TicketHistories.GetByTicket;

public sealed class TicketHistoryResponse
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
