using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.Unassign;

public sealed record UnassignTicketCommand(Guid TicketId) : ICommand;
