using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed class TicketHistory : Entity
{
    private TicketHistory(
        Guid id,
        Guid ticketId,
        Guid? changedByUserId,
        TicketHistoryAction action,
        string description,
        string? oldValue,
        string? newValue,
        DateTime occurredAtUtc)
    {
        Id = id;
        TicketId = ticketId;
        ChangedByUserId = changedByUserId;
        Action = action;
        Description = description;
        OldValue = oldValue;
        NewValue = newValue;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid? ChangedByUserId { get; private set; }
    public TicketHistoryAction Action { get; private set; }
    public string Description { get; private set; }
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>
    /// Ticket history entries are an immutable audit trail: create-only, never updated or deleted.
    /// </summary>
    public static TicketHistory Create(
        Guid ticketId,
        Guid? changedByUserId,
        TicketHistoryAction action,
        string description,
        string? oldValue,
        string? newValue,
        DateTime occurredAtUtc)
    {
        var history = new TicketHistory(
            Guid.NewGuid(),
            ticketId,
            changedByUserId,
            action,
            description,
            oldValue,
            newValue,
            occurredAtUtc);

        history.Raise(new TicketHistoryRecordedDomainEvent(history.Id, ticketId));

        return history;
    }
}
