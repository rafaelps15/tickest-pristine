using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketHistories;

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
    /// Cria o anexo sem conferências, porque o <c>Ticket.AddAttachment</c> confere o envio antes, e dispara o evento do
    /// envio.
    /// </summary>
    internal static TicketAttachment Create(
        Guid ticketId,
        Guid uploadedByUserId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string storageKey,
        DateTime utcNow)
    {
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

    /// <summary>
    /// Registra que o arquivo do anexo excluído saiu do armazenamento. Devolve <c>false</c>, sem mudar nada, quando o
    /// anexo não está excluído ou quando a purga já foi registrada.
    /// </summary>
    public bool RecordFilePurge(DateTime utcNow)
    {
        if (DeletedAtUtc is null || PurgedAtUtc is not null)
        {
            return false;
        }

        PurgedAtUtc = utcNow;

        return true;
    }
}
