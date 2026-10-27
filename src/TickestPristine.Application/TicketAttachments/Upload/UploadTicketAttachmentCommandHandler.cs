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
/// Grava o anexo de um participante do chamado. Antes de gravar, confere os limites do chamado
/// (<see cref="TicketAttachmentLimits.MaxAttachmentsPerTicket"/> anexos e
/// <see cref="TicketAttachmentLimits.MaxTotalBytesPerTicket"/> bytes somados, contando só os não excluídos) e se o
/// conteúdo corresponde ao tipo declarado; uma recusa não grava arquivo nem linha.
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

        if (usage is not null &&
            (usage.Count >= TicketAttachmentLimits.MaxAttachmentsPerTicket ||
             usage.TotalBytes + command.FileSizeBytes > TicketAttachmentLimits.MaxTotalBytesPerTicket))
        {
            return Result.Failure<Guid>(TicketAttachmentErrors.TicketLimitReached(
                TicketAttachmentLimits.MaxAttachmentsPerTicket,
                TicketAttachmentLimits.MaxTotalBytesPerTicket / (1024 * 1024)));
        }

        if (!await TicketAttachmentContentInspector.MatchesAsync(command.Content, command.ContentType, cancellationToken))
        {
            return Result.Failure<Guid>(TicketAttachmentErrors.ContentMismatch);
        }

        string storageKey = await fileStorage.SaveAsync(command.Content, command.FileName, cancellationToken);

        DateTime nowUtc = dateTimeProvider.UtcNow;

        var attachment = new TicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = command.TicketId,
            UploadedByUserId = userContext.UserId,
            FileName = command.FileName,
            ContentType = command.ContentType,
            FileSizeBytes = command.FileSizeBytes,
            StorageKey = storageKey,
            UploadedAtUtc = nowUtc
        };

        attachment.Raise(new TicketAttachmentUploadedDomainEvent(
            attachment.Id,
            attachment.TicketId,
            attachment.UploadedByUserId,
            attachment.FileName));

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
