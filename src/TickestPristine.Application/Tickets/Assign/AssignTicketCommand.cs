using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.Assign;

public sealed record AssignTicketCommand(Guid TicketId, Guid AssignedToUserId) : ICommand;
