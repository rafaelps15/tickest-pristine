using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Domain.Tickets;
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
            TicketHistoryDescriptions.AttachmentAdded(attachment.FileName),
            nowUtc));

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
