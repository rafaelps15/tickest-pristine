using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.Upload;

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
        var ticket = await context.Tickets
            .Where(t => t.Id == command.TicketId)
            .Select(t => new { t.CreatedByUserId, t.AssignedToUserId })
            .SingleOrDefaultAsync(cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<Guid>(TicketErrors.NotFound(command.TicketId));
        }

        bool isParticipant = ticket.CreatedByUserId == userContext.UserId || ticket.AssignedToUserId == userContext.UserId;

        if (!isParticipant)
        {
            bool canManageTickets = await permissionProvider.HasPermissionAsync(
                userContext.UserId,
                PermissionCodes.Tickets.Manage,
                cancellationToken);

            if (!canManageTickets)
            {
                return Result.Failure<Guid>(UserErrors.Unauthorized());
            }
        }

        string storageKey = await fileStorage.SaveAsync(command.Content, command.FileName, cancellationToken);

        var attachment = new TicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = command.TicketId,
            UploadedByUserId = userContext.UserId,
            FileName = command.FileName,
            ContentType = command.ContentType,
            FileSizeBytes = command.FileSizeBytes,
            StorageKey = storageKey,
            UploadedAtUtc = dateTimeProvider.UtcNow
        };

        attachment.Raise(new TicketAttachmentUploadedDomainEvent(attachment.Id, attachment.TicketId));

        context.TicketAttachments.Add(attachment);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DeleteFileIfNotPersistedAsync(attachment.Id, storageKey);
            throw;
        }

        return attachment.Id;
    }

    /// <summary>
    /// Apaga o arquivo gravado quando o anexo não chegou ao banco. A falha pode vir depois da gravação
    /// (nos eventos de domínio); nesse caso o anexo existe e o arquivo é mantido.
    /// </summary>
    private async Task DeleteFileIfNotPersistedAsync(Guid attachmentId, string storageKey)
    {
        bool persisted = await context.TicketAttachments
            .AsNoTracking()
            .AnyAsync(a => a.Id == attachmentId, CancellationToken.None);

        if (!persisted)
        {
            await fileStorage.DeleteAsync(storageKey, CancellationToken.None);
        }
    }
}
