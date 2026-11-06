using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketAttachments.GetContent;

public sealed record GetTicketAttachmentContentQuery(Guid AttachmentId) : IQuery<TicketAttachmentResponse>;
