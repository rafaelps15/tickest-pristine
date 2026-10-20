using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Authorization;
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

        attachment.Raise(new TicketAttachmentUploadedDomainEvent(
            attachment.Id,
            attachment.TicketId,
            attachment.UploadedByUserId,
            attachment.FileName));

        context.TicketAttachments.Add(attachment);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Sem a linha no banco, o arquivo ficaria órfão no disco. Falhas dos handlers de evento não chegam aqui:
            // o dispatcher as registra sem propagar, porque nesse ponto o anexo já foi salvo.
            await fileStorage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }

        return attachment.Id;
    }
}
