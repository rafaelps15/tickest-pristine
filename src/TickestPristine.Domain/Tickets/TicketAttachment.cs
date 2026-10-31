using TickestPristine.Domain.Users;
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

    private TicketAttachment()
    {
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid UploadedByUserId { get; private set; }

    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public long FileSizeBytes { get; private set; }

    public string StorageKey { get; private set; }

    public DateTime UploadedAtUtc { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>
    /// Quando o arquivo foi apagado do armazenamento, depois da exclusão lógica. Nulo enquanto o arquivo existir ou se a
    /// remoção falhou.
    /// </summary>
    public DateTime? PurgedAtUtc { get; private set; }

    /// <summary>
    /// Cria o anexo, ou devolve o erro quando o tipo não é aceito (os parâmetros do tipo, como o charset, são ignorados),
    /// quando o nome do arquivo não tem uma extensão do tipo, quando o arquivo passa do tamanho máximo ou quando o chamado
    /// já tem, nos anexos não excluídos, o máximo de anexos ou de bytes somados.
    /// </summary>
    public static Result<TicketAttachment> Create(
        Ticket ticket,
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
            TicketId = ticket.Id,
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
    /// Entrada do histórico do envio, em nome de quem enviou, com o nome do arquivo e a data do envio.
    /// </summary>
    public TicketHistory RecordUpload() =>
        TicketHistory.Create(TicketId, UploadedByUserId, TicketHistoryAction.AttachmentAdded, UploadedAtUtc, newValue: FileName);

    /// <summary>
    /// Exclui o anexo logicamente e devolve a entrada do histórico, em nome de quem excluiu; o arquivo é apagado do
    /// armazenamento depois, pelo handler do evento. Quem enviou sempre pode; os demais, só com
    /// <paramref name="canDeleteAny"/> (quem atende a fila). Em falha, nada muda e nenhum evento é disparado.
    /// </summary>
    public Result<TicketHistory> Delete(Guid deletedByUserId, bool canDeleteAny, DateTime utcNow)
    {
        if (deletedByUserId != UploadedByUserId && !canDeleteAny)
        {
            return Result.Failure<TicketHistory>(UserErrors.Unauthorized());
        }

        DeletedAtUtc = utcNow;

        Raise(new TicketAttachmentDeletedDomainEvent(Id, TicketId));

        return TicketHistory.Create(TicketId, deletedByUserId, TicketHistoryAction.AttachmentRemoved, utcNow);
    }

    public void MarkPurged(DateTime utcNow) => PurgedAtUtc = utcNow;

    /// <summary>
    /// Tipo sem os parâmetros: "text/plain; charset=utf-8" é o mesmo formato que "text/plain".
    /// </summary>
    public static string GetMediaType(string contentType) => contentType.Split(';', 2)[0].Trim();
}
