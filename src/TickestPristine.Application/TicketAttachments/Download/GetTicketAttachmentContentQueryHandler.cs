using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Authorization;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.TicketAttachments.Download;

internal sealed class GetTicketAttachmentContentQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IFileStorage fileStorage,
    ILogger<GetTicketAttachmentContentQueryHandler> logger)
    : IQueryHandler<GetTicketAttachmentContentQuery, TicketAttachmentResponse>
{
    public async Task<Result<TicketAttachmentResponse>> Handle(
        GetTicketAttachmentContentQuery query,
        CancellationToken cancellationToken)
    {
        var stored = await context.TicketAttachments
            .Where(a => a.Id == query.AttachmentId && context.Tickets.Any(t => t.Id == a.TicketId))
            .Select(a => new
            {
                a.TicketId,
                Response = new TicketAttachmentResponse
                {
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    StorageKey = a.StorageKey
                }
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (stored is null)
        {
            return Result.Failure<TicketAttachmentResponse>(TicketAttachmentErrors.NotFound(query.AttachmentId));
        }

        Result access = await TicketAccessGuard.EnsureCanViewAsync(
            context,
            permissionProvider,
            userContext.UserId,
            stored.TicketId,
            cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<TicketAttachmentResponse>(access.Error);
        }

        Stream? content = await fileStorage.OpenReadAsync(stored.Response.StorageKey, cancellationToken);

        if (content is null)
        {
            // A linha existe, mas o arquivo sumiu do armazenamento: falha de operação, não do cliente.
            logger.LogError("O arquivo do anexo {AttachmentId} não foi encontrado no armazenamento", query.AttachmentId);

            return Result.Failure<TicketAttachmentResponse>(TicketAttachmentErrors.FileMissing(query.AttachmentId));
        }

        stored.Response.Content = content;

        return stored.Response;
    }
}
