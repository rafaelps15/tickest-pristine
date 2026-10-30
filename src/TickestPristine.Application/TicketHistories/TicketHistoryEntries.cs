using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.TicketHistories;

/// <summary>
/// Monta os registros do histórico das mensagens e dos anexos do chamado; as mudanças do próprio chamado têm a entrada
/// criada pelos métodos de <see cref="Ticket"/>.
/// </summary>
public static class TicketHistoryEntries
{
    public static TicketHistory Create(
        Guid ticketId,
        Guid? changedByUserId,
        TicketHistoryAction action,
        DateTime occurredAtUtc,
        string? oldValue = null,
        string? newValue = null) => new()
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
