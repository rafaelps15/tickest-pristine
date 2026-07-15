using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.Reopen;

public sealed record ReopenTicketCommand(Guid TicketId) : ICommand;
