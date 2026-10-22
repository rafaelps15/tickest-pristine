using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.TicketHistories;

/// <summary>
/// Monta os registros do histórico de chamados num lugar só: os handlers dos commands gravam com a data atual, no mesmo
/// SaveChanges da mudança do chamado, e os dados de exemplo, com datas no passado.
/// </summary>
public static class TicketHistoryEntries
{
    /// <summary>
    /// Registro de qualquer ação do histórico; as ações com valores próprios têm os seus métodos abaixo.
    /// </summary>
    public static TicketHistory Create(
        Guid ticketId,
        Guid? changedByUserId,
        TicketHistoryAction action,
        string description,
        DateTime occurredAtUtc,
        string? oldValue = null,
        string? newValue = null) => new()
    {
        Id = Guid.NewGuid(),
        TicketId = ticketId,
        ChangedByUserId = changedByUserId,
        Action = action,
        Description = description,
        OldValue = oldValue,
        NewValue = newValue,
        OccurredAtUtc = occurredAtUtc
    };

    /// <summary>
    /// Abertura do chamado, que sempre nasce com o status Aberto.
    /// </summary>
    public static TicketHistory Created(Guid ticketId, Guid createdByUserId, DateTime occurredAtUtc) =>
        Create(
            ticketId,
            createdByUserId,
            TicketHistoryAction.Created,
            TicketHistoryDescriptions.Created,
            occurredAtUtc,
            newValue: TicketStatus.Open.ToString());

    public static TicketHistory StatusChanged(
        Guid ticketId,
        Guid? changedByUserId,
        TicketStatus oldStatus,
        TicketStatus newStatus,
        DateTime occurredAtUtc) =>
        Create(
            ticketId,
            changedByUserId,
            TicketHistoryAction.StatusChanged,
            TicketHistoryDescriptions.StatusChanged(oldStatus, newStatus),
            occurredAtUtc,
            oldStatus.ToString(),
            newStatus.ToString());
}
