using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketMessages.Post;

public sealed class PostTicketMessageCommand : ICommand<Guid>
{
    public Guid TicketId { get; set; }
    public string Content { get; set; }
}
