using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public static class TicketAttachmentErrors
{
    public static Error NotFound(Guid attachmentId) => Error.NotFound(
        "TicketAttachments.NotFound",
        $"O anexo com o Id = '{attachmentId}' não foi encontrado");

    public static Error FileTooLarge(long maxSizeBytes) => Error.Problem(
        "TicketAttachments.FileTooLarge",
        $"O arquivo excede o tamanho máximo permitido de {maxSizeBytes} bytes");

    public static Error UnsupportedContentType(string contentType) => Error.Problem(
        "TicketAttachments.UnsupportedContentType",
        $"O tipo de arquivo '{contentType}' não é suportado");

    public static Error Unauthorized() => Error.Forbidden(
        "TicketAttachments.Unauthorized",
        "Você não tem permissão para executar esta ação neste anexo.");
}
