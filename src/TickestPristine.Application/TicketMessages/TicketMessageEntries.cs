using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.TicketMessages;

/// <summary>
/// Grava uma mensagem no chamado num lugar só, para a publicação de mensagens e a reabertura com motivo: a mensagem, o
/// evento de domínio e o histórico <c>MessageAdded</c>. Quem chama salva o contexto, no mesmo SaveChanges da ação.
/// </summary>
internal static class TicketMessageEntries
{
    public static TicketMessage Add(
        IApplicationDbContext context,
        Guid ticketId,
        Guid authorUserId,
        string content,
        DateTime nowUtc)
    {
        var message = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = authorUserId,
            Content = content,
            CreatedAtUtc = nowUtc
        };

        message.Raise(new TicketMessagePostedDomainEvent(message.Id, message.TicketId, message.AuthorUserId));

        context.TicketMessages.Add(message);

        context.TicketHistories.Add(TicketHistoryEntries.Create(
            message.TicketId,
            message.AuthorUserId,
            TicketHistoryAction.MessageAdded,
            nowUtc));

        return message;
    }
}
