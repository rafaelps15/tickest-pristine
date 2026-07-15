using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.Tickets.Update;

public sealed class UpdateTicketCommand : ICommand
{
    public Guid TicketId { get; set; }
    public string Description { get; set; }
    public TicketStatus Status { get; set; }
}
