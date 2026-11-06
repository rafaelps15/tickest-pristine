using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketHistories;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Domain.TicketAttachments;

public sealed class TicketAttachment : Entity, IAuditable
{
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
        string[]? extensions = TicketAttachmentPolicy.ExtensionsOf(contentType);

        if (extensions is null)
        {
            return Result.Failure<TicketAttachment>(TicketAttachmentErrors.UnsupportedContentType(contentType));
        }

        if (!extensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure<TicketAttachment>(TicketAttachmentErrors.ExtensionMismatch(contentType));
        }

        if (fileSizeBytes > TicketAttachmentPolicy.MaxFileSizeBytes)
        {
            return Result.Failure<TicketAttachment>(
                TicketAttachmentErrors.FileTooLarge(TicketAttachmentPolicy.MaxFileSizeBytes / (1024 * 1024)));
        }

        if (currentCount >= TicketAttachmentPolicy.MaxAttachmentsPerTicket ||
            currentTotalBytes + fileSizeBytes > TicketAttachmentPolicy.MaxTotalBytesPerTicket)
        {
            return Result.Failure<TicketAttachment>(TicketAttachmentErrors.TicketLimitReached(
                TicketAttachmentPolicy.MaxAttachmentsPerTicket,
                TicketAttachmentPolicy.MaxTotalBytesPerTicket / (1024 * 1024)));
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
    public TicketHistoryEntry RecordUpload() =>
        TicketHistoryEntry.Create(TicketId, UploadedByUserId, TicketHistoryAction.AttachmentAdded, UploadedAtUtc, newValue: FileName);

    /// <summary>
    /// Exclui o anexo logicamente e devolve a entrada do histórico, em nome de quem excluiu; o arquivo é apagado do
    /// armazenamento depois, pelo handler do evento. Quem enviou sempre pode; os demais, só com
    /// <paramref name="canDeleteAny"/> (quem atende a fila). Em falha, nada muda e nenhum evento é disparado.
    /// </summary>
    public Result<TicketHistoryEntry> Delete(Guid deletedByUserId, bool canDeleteAny, DateTime utcNow)
    {
        if (deletedByUserId != UploadedByUserId && !canDeleteAny)
        {
            return Result.Failure<TicketHistoryEntry>(UserErrors.Unauthorized());
        }

        DeletedAtUtc = utcNow;

        Raise(new TicketAttachmentDeletedDomainEvent(Id, TicketId));

        return TicketHistoryEntry.Create(TicketId, deletedByUserId, TicketHistoryAction.AttachmentRemoved, utcNow);
    }

    public void MarkPurged(DateTime utcNow) => PurgedAtUtc = utcNow;
}
