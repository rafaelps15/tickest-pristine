using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed class TicketMessage : Entity
{
    private TicketMessage(
        Guid id,
        Guid ticketId,
        Guid authorUserId,
        string content,
        DateTime createdAtUtc)
    {
        Id = id;
        TicketId = ticketId;
        AuthorUserId = authorUserId;
        Content = content;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Content { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? EditedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }

    public static TicketMessage Create(
        Guid ticketId,
        Guid authorUserId,
        string content,
        DateTime createdAtUtc)
    {
        var message = new TicketMessage(Guid.NewGuid(), ticketId, authorUserId, content, createdAtUtc);

        message.Raise(new TicketMessagePostedDomainEvent(message.Id, ticketId));

        return message;
    }

    public void Edit(string content, DateTime editedAtUtc)
    {
        Content = content;
        EditedAtUtc = editedAtUtc;

        Raise(new TicketMessageEditedDomainEvent(Id, TicketId));
    }

    public void Delete(DateTime deletedAtUtc)
    {
        DeletedAtUtc = deletedAtUtc;

        Raise(new TicketMessageDeletedDomainEvent(Id, TicketId));
    }
}
