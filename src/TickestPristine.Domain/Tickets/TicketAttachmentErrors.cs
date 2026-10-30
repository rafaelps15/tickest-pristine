using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public static class TicketAttachmentErrors
{
    public static Error NotFound(Guid attachmentId) => Error.NotFound(
        "TicketAttachments.NotFound",
        $"O anexo com o Id = '{attachmentId}' não foi encontrado");

    public static Error UnsupportedContentType(string contentType) => Error.Problem(
        "TicketAttachments.UnsupportedContentType",
        $"O tipo de arquivo '{contentType}' não é suportado.");

    public static Error ExtensionMismatch(string contentType) => Error.Problem(
        "TicketAttachments.ExtensionMismatch",
        $"A extensão do arquivo não corresponde ao tipo '{contentType}'.");

    public static Error FileTooLarge(long maxMegabytes) => Error.Problem(
        "TicketAttachments.FileTooLarge",
        $"O arquivo excede o tamanho máximo permitido de {maxMegabytes} MB.");

    public static readonly Error ContentMismatch = Error.Problem(
        "TicketAttachments.ContentMismatch",
        "O conteúdo do arquivo não corresponde ao tipo informado.");

    public static Error TicketLimitReached(int maxAttachments, long maxTotalMegabytes) => Error.Problem(
        "TicketAttachments.TicketLimitReached",
        $"O chamado atingiu o limite de {maxAttachments} anexos ou de {maxTotalMegabytes} MB somados. Exclua um anexo antes de enviar outro.");

    public static Error FileMissing(Guid attachmentId) => Error.NotFound(
        "TicketAttachments.FileMissing",
        $"O arquivo do anexo com o Id = '{attachmentId}' não está mais disponível.");
}
