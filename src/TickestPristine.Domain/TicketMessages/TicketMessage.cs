using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketHistories;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Domain.TicketMessages;

public sealed class TicketMessage : Entity, IAuditable
{
    private TicketMessage()
    {
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string Content { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? EditedAtUtc { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>
    /// Cria a mensagem e dispara o evento de publicação; chamado só por <see cref="Ticket.PostMessage"/>, por isso é
    /// interno.
    /// </summary>
    internal static TicketMessage Create(Guid ticketId, Guid authorUserId, string content, DateTime utcNow)
    {
        var message = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = authorUserId,
            Content = content,
            CreatedAtUtc = utcNow
        };

        message.Raise(new TicketMessagePostedDomainEvent(message.Id, message.TicketId, message.AuthorUserId));

        return message;
    }

    /// <summary>
    /// Troca o conteúdo e devolve a entrada do histórico; só o autor edita. Em falha, nada muda e nenhum evento é
    /// disparado.
    /// </summary>
    public Result<TicketHistoryEntry> Edit(string content, Guid editorUserId, DateTime utcNow)
    {
        if (editorUserId != AuthorUserId)
        {
            return Result.Failure<TicketHistoryEntry>(UserErrors.Unauthorized());
        }

        string previousContent = Content;

        Content = content;
        EditedAtUtc = utcNow;

        Raise(new TicketMessageEditedDomainEvent(Id, TicketId, AuthorUserId, previousContent));

        return TicketHistoryEntry.Create(TicketId, AuthorUserId, TicketHistoryAction.MessageEdited, utcNow);
    }

    /// <summary>
    /// Exclui a mensagem logicamente e devolve a entrada do histórico, em nome de quem excluiu. O autor sempre pode;
    /// os demais, só com <paramref name="canDeleteAny"/> (quem atende a fila). Em falha, nada muda e nenhum evento é
    /// disparado.
    /// </summary>
    public Result<TicketHistoryEntry> Delete(Guid deletedByUserId, bool canDeleteAny, DateTime utcNow)
    {
        if (deletedByUserId != AuthorUserId && !canDeleteAny)
        {
            return Result.Failure<TicketHistoryEntry>(UserErrors.Unauthorized());
        }

        DeletedAtUtc = utcNow;

        Raise(new TicketMessageDeletedDomainEvent(Id, TicketId));

        return TicketHistoryEntry.Create(TicketId, deletedByUserId, TicketHistoryAction.MessageRemoved, utcNow);
    }
}
