using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Tickets;

public sealed class TicketAttachment : Entity, IAuditable
{
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
}
