namespace TickestPristine.Domain.Tickets;

/// <summary>
/// Monta os registros do histórico de chamados num lugar só: os handlers de evento de domínio gravam com a data atual
/// e os dados de exemplo, com datas no passado.
/// </summary>
public static class TicketHistoryEntries
{
    /// <summary>
    /// Abertura do chamado, que sempre nasce com o status Aberto.
    /// </summary>
    public static TicketHistory Created(Guid ticketId, Guid createdByUserId, DateTime occurredAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        TicketId = ticketId,
        ChangedByUserId = createdByUserId,
        Action = TicketHistoryAction.Created,
        Description = TicketHistoryDescriptions.Created,
        OldValue = null,
        NewValue = TicketStatus.Open.ToString(),
        OccurredAtUtc = occurredAtUtc
    };

    public static TicketHistory StatusChanged(
        Guid ticketId,
        Guid? changedByUserId,
        TicketStatus oldStatus,
        TicketStatus newStatus,
        DateTime occurredAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        TicketId = ticketId,
        ChangedByUserId = changedByUserId,
        Action = TicketHistoryAction.StatusChanged,
        Description = TicketHistoryDescriptions.StatusChanged(oldStatus, newStatus),
        OldValue = oldStatus.ToString(),
        NewValue = newStatus.ToString(),
        OccurredAtUtc = occurredAtUtc
    };
}
