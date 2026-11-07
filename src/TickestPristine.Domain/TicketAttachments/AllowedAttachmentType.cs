namespace TickestPristine.Domain.TicketAttachments;

public sealed record AllowedAttachmentType(string ContentType, IReadOnlyList<string> Extensions);
