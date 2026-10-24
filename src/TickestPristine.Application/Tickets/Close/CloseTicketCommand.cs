using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.Close;

public sealed record CloseTicketCommand(Guid TicketId) : ICommand;
