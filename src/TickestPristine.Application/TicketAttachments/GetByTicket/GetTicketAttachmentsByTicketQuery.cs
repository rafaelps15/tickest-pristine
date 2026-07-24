using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketAttachments.GetByTicket;

public sealed record GetTicketAttachmentsByTicketQuery(Guid TicketId) : IQuery<List<TicketAttachmentResponse>>;
