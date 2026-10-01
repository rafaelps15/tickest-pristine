using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketMessages.Edit;

public sealed class EditTicketMessageCommand : ICommand
{
    public Guid MessageId { get; set; }
    public string Content { get; set; }
}
