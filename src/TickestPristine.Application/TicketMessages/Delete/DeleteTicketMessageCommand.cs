using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketMessages.Delete;

public sealed record DeleteTicketMessageCommand(Guid MessageId) : ICommand;
