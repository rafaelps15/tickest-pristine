using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed class TicketAttachment : Entity, IAuditable
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
    /// Tipos aceitos e as extensões válidas de cada um, na ordem em que a política de anexos os apresenta. O nome do
    /// arquivo precisa ter uma delas: o tipo vem do cliente, e sem essa conferência um executável chegaria como
    /// "nota.pdf.exe" declarado como PDF.
    /// </summary>
    public static readonly IReadOnlyList<(string ContentType, string[] Extensions)> AllowedTypes =
    [
        ("image/png", [".png"]),
        ("image/jpeg", [".jpg", ".jpeg"]),
        ("image/gif", [".gif"]),
        ("image/webp", [".webp"]),
        ("application/pdf", [".pdf"]),
        ("application/msword", [".doc"]),
        ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [".docx"]),
        ("application/vnd.ms-excel", [".xls"]),
        ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [".xlsx"]),
        ("text/plain", [".txt"]),
        ("application/zip", [".zip"])
    ];

    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public string StorageKey { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    /// <summary>
    /// Quando o arquivo foi apagado do armazenamento, depois da exclusão lógica. Nulo enquanto o arquivo existir ou se a
    /// remoção falhou.
    /// </summary>
    public DateTime? PurgedAtUtc { get; set; }

    /// <summary>
    /// Cria o anexo, ou devolve o erro quando o tipo não é aceito (os parâmetros do tipo, como o charset, são ignorados),
    /// quando o nome do arquivo não tem uma extensão do tipo, quando o arquivo passa do tamanho máximo ou quando o chamado
    /// já tem, nos anexos não excluídos, o máximo de anexos ou de bytes somados.
    /// </summary>
    public static Result<TicketAttachment> Create(
        Guid ticketId,
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string storageKey,
        int currentCount,
        long currentTotalBytes,
        DateTime utcNow)
    {
        string mediaType = GetMediaType(contentType);
        (string ContentType, string[] Extensions) allowedType = AllowedTypes
            .FirstOrDefault(type => string.Equals(type.ContentType, mediaType, StringComparison.OrdinalIgnoreCase));

        if (allowedType.ContentType is null)
        {
            return Result.Failure<TicketAttachment>(TicketAttachmentErrors.UnsupportedContentType(contentType));
        }

        if (!allowedType.Extensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure<TicketAttachment>(TicketAttachmentErrors.ExtensionMismatch(contentType));
        }

        if (fileSizeBytes > MaxFileSizeBytes)
        {
            return Result.Failure<TicketAttachment>(TicketAttachmentErrors.FileTooLarge(MaxFileSizeBytes / (1024 * 1024)));
        }

        if (currentCount >= MaxAttachmentsPerTicket || currentTotalBytes + fileSizeBytes > MaxTotalBytesPerTicket)
        {
            return Result.Failure<TicketAttachment>(TicketAttachmentErrors.TicketLimitReached(
                MaxAttachmentsPerTicket,
                MaxTotalBytesPerTicket / (1024 * 1024)));
        }

        var attachment = new TicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            UploadedByUserId = uploadedByUserId,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            StorageKey = storageKey,
            UploadedAtUtc = utcNow
        };

        attachment.Raise(new TicketAttachmentUploadedDomainEvent(
            attachment.Id,
            attachment.TicketId,
            attachment.UploadedByUserId,
            attachment.FileName));

        return attachment;
    }

    /// <summary>
    /// Tipo sem os parâmetros: "text/plain; charset=utf-8" é o mesmo formato que "text/plain".
    /// </summary>
    public static string GetMediaType(string contentType) => contentType.Split(';', 2)[0].Trim();
}
