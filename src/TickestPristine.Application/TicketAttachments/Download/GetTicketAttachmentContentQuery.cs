using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketAttachments.Download;

public sealed record GetTicketAttachmentContentQuery(Guid AttachmentId) : IQuery<TicketAttachmentDownloadResponse>;
