using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketAttachments.Delete;

public sealed record DeleteTicketAttachmentCommand(Guid AttachmentId) : ICommand;
