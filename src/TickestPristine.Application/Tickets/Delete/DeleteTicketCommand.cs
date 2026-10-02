using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.Delete;

public sealed record DeleteTicketCommand(Guid TicketId) : ICommand;
