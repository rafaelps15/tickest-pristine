using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.Upload;

/// <summary>
/// Grava o anexo de um participante do chamado. O conteúdo é conferido contra o tipo declarado antes de ir para o
/// armazenamento; as regras do anexo (tipo, extensão, tamanho e os limites do chamado, contando só os anexos não
/// excluídos) são do <see cref="TicketAttachment.Create"/>, e uma recusa dele apaga o arquivo gravado. Uma recusa não
/// deixa arquivo nem linha.
/// </summary>
internal sealed class UploadTicketAttachmentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider,
    IFileStorage fileStorage)
    : ICommandHandler<UploadTicketAttachmentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UploadTicketAttachmentCommand command, CancellationToken cancellationToken)
    {
        Result access = await TicketAccessGuard.EnsureCanViewAsync(
            context,
            permissionProvider,
            userContext.UserId,
            command.TicketId,
            cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<Guid>(access.Error);
        }

        // O filtro global exclui os anexos excluídos: só os que continuam no chamado contam para o limite.
        var usage = await context.TicketAttachments
            .Where(a => a.TicketId == command.TicketId)
            .GroupBy(a => a.TicketId)
            .Select(g => new { Count = g.Count(), TotalBytes = g.Sum(a => a.FileSizeBytes) })
            .SingleOrDefaultAsync(cancellationToken);

        if (!await TicketAttachmentContentInspector.MatchesAsync(command.Content, command.ContentType, cancellationToken))
        {
            return Result.Failure<Guid>(TicketAttachmentErrors.ContentMismatch);
        }

        string storageKey = await fileStorage.SaveAsync(command.Content, command.FileName, cancellationToken);

        DateTime nowUtc = dateTimeProvider.UtcNow;

        Result<TicketAttachment> created = TicketAttachment.Create(
            command.TicketId,
            userContext.UserId,
            command.FileName,
            command.ContentType,
            command.FileSizeBytes,
            storageKey,
            usage?.Count ?? 0,
            usage?.TotalBytes ?? 0,
            nowUtc);

        if (created.IsFailure)
        {
            // O arquivo já foi gravado para obter a chave: sem o anexo, ele ficaria órfão no disco.
            await fileStorage.DeleteAsync(storageKey, CancellationToken.None);

            return Result.Failure<Guid>(created.Error);
        }

        TicketAttachment attachment = created.Value;

        context.TicketAttachments.Add(attachment);

        context.TicketHistories.Add(TicketHistoryEntries.Create(
            attachment.TicketId,
            attachment.UploadedByUserId,
            TicketHistoryAction.AttachmentAdded,
            nowUtc,
            newValue: attachment.FileName));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Sem a linha no banco, o arquivo ficaria órfão no disco.
            await fileStorage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }

        return attachment.Id;
    }
}
