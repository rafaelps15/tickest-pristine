using System.Data.Common;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetAll;

internal sealed class GetTicketsQueryHandler(IDbConnectionFactory dbConnectionFactory, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTicketsQuery, PagedResponse<TicketSummaryResponse>>
{
    public async Task<Result<PagedResponse<TicketSummaryResponse>>> Handle(
        GetTicketsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Scope.HasInvertedPeriod)
        {
            return Result.Failure<PagedResponse<TicketSummaryResponse>>(TicketErrors.InvalidPeriod);
        }

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // O título é citext: a busca não diferencia maiúsculas, e o termo vai como padrão escapado (%, _ e \ contam como
        // texto). A situação de SLA pedida escolhe o trecho com nome dela; sem situação, a listagem não filtra pelo SLA,
        // e qualquer outro valor filtra como no prazo.
        const string filter =
            $"""
             {TicketSql.MatchesScope}
               AND (cardinality(@Statuses) = 0 OR t.status = ANY(@Statuses))
               AND (@WaitingOn::text IS NULL OR t.waiting_on = @WaitingOn::text)
               AND (@SearchPattern::text IS NULL OR t.title LIKE @SearchPattern::text)
               AND (@SlaStatus::text IS NULL
                    OR (@SlaStatus::text = '{nameof(TicketSlaStatus.AtRisk)}' AND {TicketSql.IsSlaAtRisk})
                    OR (@SlaStatus::text = '{nameof(TicketSlaStatus.Breached)}' AND {TicketSql.IsSlaBreached})
                    OR (@SlaStatus::text = '{nameof(TicketSlaStatus.Met)}' AND {TicketSql.IsSlaMet})
                    OR (@SlaStatus::text = '{nameof(TicketSlaStatus.Missed)}' AND {TicketSql.IsSlaMissed})
                    OR (@SlaStatus::text = '{nameof(TicketSlaStatus.NotApplicable)}' AND {TicketSql.IsSlaNotApplicable})
                    OR (@SlaStatus::text NOT IN ('{nameof(TicketSlaStatus.AtRisk)}', '{nameof(TicketSlaStatus.Breached)}',
                                                 '{nameof(TicketSlaStatus.Met)}', '{nameof(TicketSlaStatus.Missed)}',
                                                 '{nameof(TicketSlaStatus.NotApplicable)}')
                        AND {TicketSql.IsSlaOnTime}))
             """;

        // O solicitante é obrigatório (chave estrangeira), então a junção não descarta chamados. As junções à esquerda só
        // trazem nomes e não mudam as linhas, por isso ficam fora da contagem.
        const string countAndPage =
            $"""
             SELECT COUNT(*)::int
             FROM public.tickets t
             JOIN public.users requester ON requester.id = t.created_by_user_id
             WHERE {filter};

             SELECT
                 t.id AS {nameof(TicketSummaryResponse.Id)},
                 t.title AS {nameof(TicketSummaryResponse.Title)},
                 t.priority AS {nameof(TicketSummaryResponse.Priority)},
                 t.status AS {nameof(TicketSummaryResponse.Status)},
                 t.created_by_user_id AS {nameof(TicketSummaryResponse.RequesterId)},
                 requester.first_name || ' ' || requester.last_name AS {nameof(TicketSummaryResponse.RequesterName)},
                 requester.email AS {nameof(TicketSummaryResponse.RequesterEmail)},
                 t.assigned_to_user_id AS {nameof(TicketSummaryResponse.AssignedToUserId)},
                 assignee.first_name || ' ' || assignee.last_name AS {nameof(TicketSummaryResponse.AssignedToName)},
                 t.sector_id AS {nameof(TicketSummaryResponse.SectorId)},
                 s.name AS {nameof(TicketSummaryResponse.SectorName)},
                 t.created_at_utc AS {nameof(TicketSummaryResponse.CreatedAtUtc)},
                 t.due_at_utc AS {nameof(TicketSummaryResponse.DueAtUtc)},
                 t.sla_warning_at_utc AS {nameof(TicketSummaryResponse.SlaWarningAtUtc)},
                 t.resolved_at_utc AS {nameof(TicketSummaryResponse.ResolvedAtUtc)},
                 {TicketSql.SlaStatus} AS {nameof(TicketSummaryResponse.SlaStatus)},
                 t.waiting_on AS {nameof(TicketSummaryResponse.WaitingOn)}
             FROM public.tickets t
             JOIN public.users requester ON requester.id = t.created_by_user_id
             LEFT JOIN public.users assignee ON assignee.id = t.assigned_to_user_id
             LEFT JOIN public.sectors s ON s.id = t.sector_id
             WHERE {filter}
             """;

        const string newestFirstSql =
            $"""
             {countAndPage}
             ORDER BY t.created_at_utc DESC, t.id DESC
             LIMIT @Limit OFFSET @Offset
             """;

        const string dueDateFirstSql =
            $"""
             {countAndPage}
             ORDER BY t.due_at_utc, t.id
             LIMIT @Limit OFFSET @Offset
             """;

        string sql = request.Sort switch
        {
            TicketSortOrder.DueDate => dueDateFirstSql,
            _ => newestFirstSql
        };

        var created = LocalDatePeriod.FromLocalDates(request.Scope.CreatedFrom, request.Scope.CreatedTo);
        var resolved = LocalDatePeriod.FromLocalDates(request.Scope.ResolvedFrom, request.Scope.ResolvedTo);
        string? search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();

        PagedResponse<TicketSummaryResponse> tickets = await connection.QueryPagedAsync<TicketSummaryResponse>(
            sql,
            new
            {
                SectorIds = request.Scope.SectorIds.ToArray(),
                request.Scope.DepartmentId,
                Priority = request.Scope.Priority?.ToString(),
                request.Scope.RequesterId,
                request.Scope.UnassignedOnly,
                request.Scope.AssignedToUserId,
                CreatedFromUtc = created.FromUtc,
                CreatedUntilUtc = created.UntilUtc,
                ResolvedFromUtc = resolved.FromUtc,
                ResolvedUntilUtc = resolved.UntilUtc,
                Statuses = request.Statuses.Select(status => status.ToString()).ToArray(),
                WaitingOn = request.WaitingOn?.ToString(),
                SearchPattern = search is null ? null : SqlLikePattern.Containing(search),
                SlaStatus = request.SlaStatus?.ToString(),
                NowUtc = dateTimeProvider.UtcNow
            },
            request.Paging,
            cancellationToken);

        return tickets;
    }
}
