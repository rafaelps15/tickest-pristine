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

    /// <summary>
    /// Abertura do chamado, que sempre nasce com o status Aberto.
    /// </summary>
    public static TicketHistory Created(Guid ticketId, Guid createdByUserId, DateTime occurredAtUtc) =>
        Create(
            ticketId,
            createdByUserId,
            TicketHistoryAction.Created,
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
            occurredAtUtc,
            oldStatus.ToString(),
            newStatus.ToString());

    /// <summary>
    /// Primeira atribuição, com o chamado sem responsável: guarda só o id do novo responsável.
    /// </summary>
    public static TicketHistory Assigned(
        Guid ticketId,
        Guid? changedByUserId,
        Guid newAssignedToUserId,
        DateTime occurredAtUtc) =>
        Create(
            ticketId,
            changedByUserId,
            TicketHistoryAction.Assigned,
            occurredAtUtc,
            newValue: newAssignedToUserId.ToString());

    /// <summary>
    /// Troca de responsável: guarda os ids do anterior e do novo. Registros gravados antes da ação
    /// <see cref="TicketHistoryAction.Assigned"/> têm o anterior nulo também na primeira atribuição.
    /// </summary>
    public static TicketHistory Reassigned(
        Guid ticketId,
        Guid? changedByUserId,
        Guid? oldAssignedToUserId,
        Guid newAssignedToUserId,
        DateTime occurredAtUtc) =>
        Create(
            ticketId,
            changedByUserId,
            TicketHistoryAction.Reassigned,
            occurredAtUtc,
            oldAssignedToUserId?.ToString(),
            newAssignedToUserId.ToString());

    /// <summary>
    /// Remoção do responsável: guarda o id do anterior, e o chamado fica sem ninguém (sem valor novo).
    /// </summary>
    public static TicketHistory Unassigned(
        Guid ticketId,
        Guid? changedByUserId,
        Guid oldAssignedToUserId,
        DateTime occurredAtUtc) =>
        Create(
            ticketId,
            changedByUserId,
            TicketHistoryAction.Unassigned,
            occurredAtUtc,
            oldAssignedToUserId.ToString());

    /// <summary>
    /// Alteração da descrição: guarda o texto anterior e o novo (os dois cabem nas colunas, como a descrição).
    /// </summary>
    public static TicketHistory DescriptionChanged(
        Guid ticketId,
        Guid? changedByUserId,
        string oldDescription,
        string newDescription,
        DateTime occurredAtUtc) =>
        Create(
            ticketId,
            changedByUserId,
            TicketHistoryAction.DescriptionChanged,
            occurredAtUtc,
            oldDescription,
            newDescription);
}
