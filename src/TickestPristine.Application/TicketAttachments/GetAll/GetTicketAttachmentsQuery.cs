using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketAttachments.GetAll;

public sealed record GetTicketAttachmentsQuery(Guid TicketId) : IQuery<List<TicketAttachmentResponse>>;
