using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.Delete;

/// <summary>
/// Apaga do armazenamento o arquivo do anexo excluído e registra quando, em <c>PurgedAtUtc</c>. Roda depois do commit da
/// exclusão lógica, que continua valendo se a remoção falhar: a falha fica no log do despacho dos eventos, com
/// <c>PurgedAtUtc</c> nulo. Arquivo já ausente não é erro.
/// </summary>
internal sealed class TicketAttachmentDeletedDomainEventHandler(
    IApplicationDbContext context,
    IFileStorage fileStorage,
    IDateTimeProvider dateTimeProvider)
    : IDomainEventHandler<TicketAttachmentDeletedDomainEvent>
{
    public async Task Handle(TicketAttachmentDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // O anexo já está excluído logicamente: só aparece ignorando o filtro global.
        TicketAttachment? attachment = await context.TicketAttachments
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(a => a.Id == domainEvent.AttachmentId, cancellationToken);

        if (attachment is null || attachment.PurgedAtUtc is not null)
        {
            return;
        }

        await fileStorage.DeleteAsync(attachment.StorageKey, cancellationToken);

        attachment.MarkPurged(dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);
    }
}
