using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketAttachments.Download;

public sealed record DownloadTicketAttachmentQuery(Guid AttachmentId) : IQuery<TicketAttachmentDownloadResponse>;
