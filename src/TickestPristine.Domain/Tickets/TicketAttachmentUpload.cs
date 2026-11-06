using TickestPristine.Domain.TicketAttachments;
using TickestPristine.Domain.TicketHistories;

namespace TickestPristine.Domain.Tickets;

public sealed record TicketAttachmentUpload(TicketAttachment Attachment, TicketHistoryEntry History);
