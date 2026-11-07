using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.TicketAttachments;

/// <summary>
/// Política dos anexos de chamado: os tipos aceitos, com as extensões de cada um, e os limites de tamanho e de
/// quantidade.
/// </summary>
public static class TicketAttachmentPolicy
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Anexos não excluídos que um chamado pode ter.
    /// </summary>
    public const int MaxAttachmentsPerTicket = 20;

    /// <summary>
    /// Soma dos tamanhos dos anexos não excluídos de um chamado.
    /// </summary>
    public const long MaxTotalBytesPerTicket = 100 * 1024 * 1024;

    /// <summary>
    /// Tipos aceitos, com as extensões válidas de cada um, na ordem em que a política os apresenta.
    /// </summary>
    public static IReadOnlyList<AllowedAttachmentType> AllowedTypes { get; } =
    [
        new("image/png", [".png"]),
        new("image/jpeg", [".jpg", ".jpeg"]),
        new("image/gif", [".gif"]),
        new("image/webp", [".webp"]),
        new("application/pdf", [".pdf"]),
        new("application/msword", [".doc"]),
        new("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [".docx"]),
        new("application/vnd.ms-excel", [".xls"]),
        new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [".xlsx"]),
        new("text/plain", [".txt"]),
        new("application/zip", [".zip"])
    ];

    /// <summary>
    /// O tipo aceito que corresponde ao informado, sem os parâmetros do tipo: "text/plain; charset=utf-8" é o
    /// "text/plain" da política. Nulo quando a política não aceita o tipo.
    /// </summary>
    public static AllowedAttachmentType? GetAllowedType(string contentType)
    {
        string mediaType = contentType.Split(';', 2)[0].Trim();

        return AllowedTypes.FirstOrDefault(type =>
            string.Equals(type.ContentType, mediaType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Confere o arquivo, nesta ordem: o tipo aceito, uma extensão do tipo no nome do arquivo (o tipo vem do cliente, e
    /// sem isso um executável chegaria como "nota.pdf.exe" declarado como PDF) e o tamanho até o máximo. Devolve o
    /// primeiro erro encontrado.
    /// </summary>
    public static Result EnsureAccepted(string fileName, string contentType, long fileSizeBytes)
    {
        AllowedAttachmentType? allowedType = GetAllowedType(contentType);

        if (allowedType is null)
        {
            return Result.Failure(TicketAttachmentErrors.UnsupportedContentType(contentType));
        }

        if (!allowedType.Extensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(TicketAttachmentErrors.ExtensionMismatch(contentType));
        }

        if (fileSizeBytes > MaxFileSizeBytes)
        {
            return Result.Failure(TicketAttachmentErrors.FileTooLarge(MaxFileSizeBytes / (1024 * 1024)));
        }

        return Result.Success();
    }
}
