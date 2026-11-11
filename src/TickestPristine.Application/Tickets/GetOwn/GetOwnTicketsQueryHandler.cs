using System.Data.Common;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Domain.Permissions;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetOwn;

internal sealed class GetOwnTicketsQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetOwnTicketsQuery, PagedResponse<OwnTicketResponse>>
{
    public async Task<Result<PagedResponse<OwnTicketResponse>>> Handle(
        GetOwnTicketsQuery request,
        CancellationToken cancellationToken)
    {
        // Uma consulta de permissão por requisição, e não por item: todos os chamados da lista são de quem consulta.
        bool canCancelAny = await permissionProvider.HasPermissionAsync(
            userContext.UserId, PermissionCodes.Tickets.Cancel, cancellationToken);
        bool canEditOwn = await permissionProvider.HasPermissionAsync(
            userContext.UserId, PermissionCodes.Tickets.UpdateOwn, cancellationToken);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // O grupo de status sai do trecho com nome do chamado ativo: finalizado é o que não está ativo (hoje, Resolvido,
        // Fechado e Cancelado), e sem grupo, ou com um grupo desconhecido, a lista traz todos os status. Título e descrição
        // são citext: a busca não diferencia maiúsculas, como na listagem geral.
        const string filter =
            $"""
             t.deleted_at_utc IS NULL
               AND t.created_by_user_id = @UserId
               AND (@StatusGroup::text IS NULL
                    OR (@StatusGroup::text = '{nameof(OwnTicketStatusGroup.Active)}' AND {TicketSql.IsActive})
                    OR (@StatusGroup::text = '{nameof(OwnTicketStatusGroup.Finished)}' AND NOT ({TicketSql.IsActive}))
                    OR @StatusGroup::text NOT IN ('{nameof(OwnTicketStatusGroup.Active)}', '{nameof(OwnTicketStatusGroup.Finished)}'))
               AND (@SearchPattern::text IS NULL
                    OR t.title LIKE @SearchPattern::text
                    OR t.description LIKE @SearchPattern::text)
             """;

        // O prazo e a situação do próprio chamado continuam os do primeiro ciclo; o prazo e a situação atuais são os do
        // ciclo vigente da resolução e, sem cronômetro de resolução (plano legado), os do chamado.
        const string sql =
            $"""
             SELECT COUNT(*)::int
             FROM public.tickets t
             WHERE {filter};

             SELECT
                 t.id AS {nameof(OwnTicketResponse.Id)},
                 t.title AS {nameof(OwnTicketResponse.Title)},
                 t.description AS {nameof(OwnTicketResponse.Description)},
                 t.priority AS {nameof(OwnTicketResponse.Priority)},
                 t.status AS {nameof(OwnTicketResponse.Status)},
                 t.created_by_user_id AS {nameof(OwnTicketResponse.OpenedByUserId)},
                 t.assigned_to_user_id AS {nameof(OwnTicketResponse.AssignedToUserId)},
                 assignee.first_name || ' ' || assignee.last_name AS {nameof(OwnTicketResponse.AssignedToName)},
                 t.sector_id AS {nameof(OwnTicketResponse.SectorId)},
                 s.name AS {nameof(OwnTicketResponse.SectorName)},
                 t.created_at_utc AS {nameof(OwnTicketResponse.CreatedAtUtc)},
                 t.due_at_utc AS {nameof(OwnTicketResponse.DueAtUtc)},
                 {TicketSql.SlaStatus} AS {nameof(OwnTicketResponse.SlaStatus)},
                 COALESCE(current_timer.due_on_utc, t.due_at_utc) AS {nameof(OwnTicketResponse.CurrentDueAtUtc)},
                 COALESCE({TicketSql.CurrentResolutionSlaStatus}, {TicketSql.SlaStatus}) AS {nameof(OwnTicketResponse.CurrentSlaStatus)},
                 t.last_activity_at_utc AS {nameof(OwnTicketResponse.LastActivityAtUtc)},
                 {TicketSql.CanBeCanceled} AS {nameof(OwnTicketResponse.CanCancel)},
                 {TicketSql.OwnerCancelDeadline} AS {nameof(OwnTicketResponse.OwnerCancelDeadlineUtc)}
             FROM public.tickets t
             LEFT JOIN public.users assignee ON assignee.id = t.assigned_to_user_id
             LEFT JOIN public.sectors s ON s.id = t.sector_id
             {TicketSql.CurrentResolutionTimer}
             WHERE {filter}
             ORDER BY t.last_activity_at_utc DESC, t.id DESC
             LIMIT @Limit OFFSET @Offset
             """;

        string? search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();

        PagedResponse<OwnTicketResponse> tickets = await connection.QueryPagedAsync<OwnTicketResponse>(
            sql,
            new
            {
                userContext.UserId,
                StatusGroup = request.StatusGroup?.ToString(),
                SearchPattern = search is null ? null : SqlLikePattern.Containing(search),
                NowUtc = dateTimeProvider.UtcNow,
                CallerId = userContext.UserId,
                CanCancelAny = canCancelAny,
                CanEditOwn = canEditOwn,
                Ticket.OwnerCancelWindowMinutes
            },
            request.Paging,
            cancellationToken);

        return tickets;
    }
}

