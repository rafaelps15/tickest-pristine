using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Tickets;
using Microsoft.Extensions.Logging;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketAttachments;

namespace TickestPristine.Application.TicketAttachments.GetContent;

internal sealed class GetTicketAttachmentContentQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IUserContext userContext,
    TicketViewAccess ticketViewAccess,
    IFileStorage fileStorage,
    ILogger<GetTicketAttachmentContentQueryHandler> logger)
    : IQueryHandler<GetTicketAttachmentContentQuery, TicketAttachmentResponse>
{
    public async Task<Result<TicketAttachmentResponse>> Handle(
        GetTicketAttachmentContentQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            $"""
             SELECT
                 a.ticket_id AS {nameof(TicketAttachmentResponse.TicketId)},
                 a.file_name AS {nameof(TicketAttachmentResponse.FileName)},
                 a.content_type AS {nameof(TicketAttachmentResponse.ContentType)},
                 a.storage_key AS {nameof(TicketAttachmentResponse.StorageKey)}
             FROM public.ticket_attachments a
             JOIN public.tickets t ON t.id = a.ticket_id
             WHERE a.id = @AttachmentId
               AND a.deleted_at_utc IS NULL
               AND t.deleted_at_utc IS NULL
             """;

        TicketAttachmentResponse? attachment = await connection.QuerySingleOrDefaultAsync<TicketAttachmentResponse>(
            new CommandDefinition(sql, request, cancellationToken: cancellationToken));

        if (attachment is null)
        {
            return Result.Failure<TicketAttachmentResponse>(TicketAttachmentErrors.NotFound(request.AttachmentId));
        }

        Result access = await ticketViewAccess.EnsureCanViewAsync(connection, userContext.UserId, attachment.TicketId, cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<TicketAttachmentResponse>(access.Error);
        }

        // O conteúdo não sai do SQL: vem do armazenamento de arquivos, pela chave gravada.
        Stream? content = await fileStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);

        if (content is null)
        {
            // A linha existe, mas o arquivo sumiu do armazenamento: falha de operação, não do cliente.
            logger.LogError("O arquivo do anexo {AttachmentId} não foi encontrado no armazenamento", request.AttachmentId);

            return Result.Failure<TicketAttachmentResponse>(TicketAttachmentErrors.FileMissing(request.AttachmentId));
        }

        attachment.Content = content;

        return attachment;
    }
}
