using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public static class TicketAttachmentErrors
{
    public static Error NotFound(Guid attachmentId) => Error.NotFound(
        "TicketAttachments.NotFound",
        $"O anexo com o Id = '{attachmentId}' não foi encontrado");
}
