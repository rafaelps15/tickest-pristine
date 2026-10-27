using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.TicketAttachments.Upload;

public sealed class UploadTicketAttachmentCommand : ICommand<Guid>
{
    public Guid TicketId { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    /// <summary>
    /// Tamanho do conteúdo recebido, medido pelo servidor, e não um valor informado pelo cliente.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Conteúdo do arquivo; precisa ser posicionável, porque o começo é lido para conferir o tipo antes de gravar.
    /// </summary>
    public Stream Content { get; set; }
}
