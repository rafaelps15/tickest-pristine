using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed class TicketHistory : Entity
{
    private TicketHistory()
    {
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid? ChangedByUserId { get; private set; }

    public TicketHistoryAction Action { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>
    /// Cria a entrada; chamado só por quem muda o chamado, uma mensagem ou um anexo (<see cref="Ticket"/>,
    /// <see cref="TicketMessage"/> e <see cref="TicketAttachment"/>), por isso é interno.
    /// </summary>
    internal static TicketHistory Create(
        Guid ticketId,
        Guid changedByUserId,
        TicketHistoryAction action,
        DateTime occurredAtUtc,
        string? oldValue = null,
        string? newValue = null)
    {
        return new TicketHistory
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            ChangedByUserId = changedByUserId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            OccurredAtUtc = occurredAtUtc
        };
    }
}
