using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.Upload;

/// <summary>
/// Grava o anexo de um participante do chamado. O chamado confere o envio antes de qualquer gravação
/// (<see cref="Ticket.AddAttachment"/>): o conteúdo contra o tipo declarado, a política dos anexos e os limites do
/// chamado, contando só os anexos não excluídos. Uma recusa não deixa arquivo nem linha; se o save falha, o arquivo
/// gravado é apagado.
/// </summary>
internal sealed class UploadTicketAttachmentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider,
    IFileStorage fileStorage,
    IAttachmentContentInspector contentInspector)
    : ICommandHandler<UploadTicketAttachmentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UploadTicketAttachmentCommand command, CancellationToken cancellationToken)
    {
        // Sem rastreio: o envio não muda o chamado; a última atividade sai da projeção, depois do commit.
        Ticket? ticket = await context.Tickets
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == command.TicketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<Guid>(TicketErrors.NotFound(command.TicketId));
        }

        bool canManageTickets = await permissionProvider.HasPermissionAsync(
            userContext.UserId,
            PermissionCodes.Tickets.Manage,
            cancellationToken);

        if (!ticket.CanBeViewedBy(userContext.UserId, canManageTickets))
        {
            return Result.Failure<Guid>(UserErrors.Unauthorized());
        }

        // O filtro global exclui os anexos excluídos: só os que continuam no chamado contam para o limite.
        var usage = await context.TicketAttachments
            .Where(a => a.TicketId == command.TicketId)
            .GroupBy(a => a.TicketId)
            .Select(g => new { Count = g.Count(), TotalBytes = g.Sum(a => a.FileSizeBytes) })
            .SingleOrDefaultAsync(cancellationToken);

        bool contentMatches = await contentInspector.MatchesAsync(command.Content, command.ContentType, cancellationToken);

        // A chave vem antes da gravação: o chamado confere o envio, e uma recusa não chega ao armazenamento.
        string storageKey = fileStorage.CreateKey(command.FileName);

        Result<TicketAttachmentUpload> upload = ticket.AddAttachment(
            userContext.UserId,
            command.FileName,
            command.ContentType,
            command.FileSizeBytes,
            storageKey,
            contentMatches,
            usage?.Count ?? 0,
            usage?.TotalBytes ?? 0,
            dateTimeProvider.UtcNow);

        if (upload.IsFailure)
        {
            return Result.Failure<Guid>(upload.Error);
        }

        await fileStorage.SaveAsync(command.Content, storageKey, cancellationToken);

        context.TicketAttachments.Add(upload.Value.Attachment);
        context.TicketHistories.Add(upload.Value.History);

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

        return upload.Value.Attachment.Id;
    }
}
