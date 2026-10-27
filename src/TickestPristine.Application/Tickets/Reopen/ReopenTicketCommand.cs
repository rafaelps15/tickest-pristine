using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Tickets.Reopen;

/// <summary>
/// Reabre um chamado encerrado. O motivo é opcional: preenchido, vira uma mensagem de quem reabriu, gravada junto com a
/// reabertura (spec 016).
/// </summary>
public sealed record ReopenTicketCommand(Guid TicketId, string? Reason = null) : ICommand;
