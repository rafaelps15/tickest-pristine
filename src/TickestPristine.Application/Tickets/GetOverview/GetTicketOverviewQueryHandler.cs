using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetOverview;

/// <summary>
/// Conta tudo num só comando, com contagens condicionais sobre os chamados do recorte. As situações de SLA e o chamado
/// ativo saem dos trechos com nome de <see cref="TicketSql"/>, os mesmos da listagem, e os testes de concordância
/// conferem que eles concordam com as regras do chamado.
/// </summary>
internal sealed class GetTicketOverviewQueryHandler(IDbConnectionFactory dbConnectionFactory, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTicketOverviewQuery, TicketOverviewResponse>
{
    public async Task<Result<TicketOverviewResponse>> Handle(
        GetTicketOverviewQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Scope.HasInvertedPeriod)
        {
            return Result.Failure<TicketOverviewResponse>(TicketErrors.InvalidPeriod);
        }

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // Uma linha só, com os blocos da resposta lado a lado: cada bloco começa na sua coluna de corte (splitOn), e o
        // instante usado nas situações de SLA vem antes deles.
        const string sql =
            $"""
             SELECT
                 @NowUtc::timestamptz AS {nameof(TicketOverviewResponse.ServerNowUtc)},
                 COUNT(*)::int AS {nameof(TicketOverviewVolumeResponse.TotalCount)},
                 COUNT(*) FILTER (WHERE t.status = '{nameof(TicketStatus.Open)}')::int AS {nameof(TicketOverviewVolumeResponse.OpenCount)},
                 COUNT(*) FILTER (WHERE t.status = '{nameof(TicketStatus.InProgress)}')::int AS {nameof(TicketOverviewVolumeResponse.InProgressCount)},
                 COUNT(*) FILTER (WHERE t.status = '{nameof(TicketStatus.Resolved)}')::int AS {nameof(TicketOverviewVolumeResponse.ResolvedCount)},
                 COUNT(*) FILTER (WHERE t.status = '{nameof(TicketStatus.Closed)}')::int AS {nameof(TicketOverviewVolumeResponse.ClosedCount)},
                 COUNT(*) FILTER (WHERE t.status = '{nameof(TicketStatus.Canceled)}')::int AS {nameof(TicketOverviewVolumeResponse.CanceledCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsActive})::int AS {nameof(TicketOverviewVolumeResponse.BacklogCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsSlaOnTime})::int AS {nameof(TicketOverviewSlaResponse.OnTimeCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsSlaAtRisk})::int AS {nameof(TicketOverviewSlaResponse.AtRiskCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsSlaBreached})::int AS {nameof(TicketOverviewSlaResponse.BreachedCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsSlaMet})::int AS {nameof(TicketOverviewSlaResponse.MetCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsSlaMissed})::int AS {nameof(TicketOverviewSlaResponse.MissedCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsSlaNotApplicable})::int AS {nameof(TicketOverviewSlaResponse.NotApplicableCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsActive} AND t.priority = '{nameof(TicketPriority.Urgent)}')::int AS {nameof(TicketOverviewAttentionResponse.CriticalCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsActive} AND t.assigned_to_user_id IS NULL)::int AS {nameof(TicketOverviewAttentionResponse.UnassignedCount)},
                 COUNT(*) FILTER (WHERE t.waiting_on = '{nameof(TicketWaitingOn.Requester)}')::int AS {nameof(TicketOverviewAttentionResponse.WaitingOnRequesterCount)},
                 COUNT(*) FILTER (WHERE t.waiting_on = '{nameof(TicketWaitingOn.Agent)}')::int AS {nameof(TicketOverviewAttentionResponse.WaitingOnAgentCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsSlaBreached})::int AS {nameof(TicketOverviewAttentionResponse.BreachedCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsActive} AND t.created_at_utc >= @OneDayAgoStartUtc::timestamptz)::int AS {nameof(TicketOverviewAgingResponse.UpToOneDayCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsActive} AND t.created_at_utc >= @ThreeDaysAgoStartUtc::timestamptz AND t.created_at_utc < @OneDayAgoStartUtc::timestamptz)::int AS {nameof(TicketOverviewAgingResponse.TwoToThreeDaysCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsActive} AND t.created_at_utc >= @SevenDaysAgoStartUtc::timestamptz AND t.created_at_utc < @ThreeDaysAgoStartUtc::timestamptz)::int AS {nameof(TicketOverviewAgingResponse.FourToSevenDaysCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsActive} AND t.created_at_utc < @SevenDaysAgoStartUtc::timestamptz)::int AS {nameof(TicketOverviewAgingResponse.OverSevenDaysCount)}
             FROM public.tickets t
             WHERE {TicketSql.MatchesScope}
             """;

        const string splitOn =
            $"{nameof(TicketOverviewVolumeResponse.TotalCount)},{nameof(TicketOverviewSlaResponse.OnTimeCount)}," +
            $"{nameof(TicketOverviewAttentionResponse.CriticalCount)},{nameof(TicketOverviewAgingResponse.UpToOneDayCount)}";

        DateTime nowUtc = dateTimeProvider.UtcNow;
        var created = LocalDatePeriod.FromLocalDates(request.Scope.CreatedFrom, request.Scope.CreatedTo);
        var resolved = LocalDatePeriod.FromLocalDates(request.Scope.ResolvedFrom, request.Scope.ResolvedTo);

        // A idade de um chamado ativo é a diferença em dias de calendário de São Paulo entre hoje e a data de abertura
        // (aberto hoje = 0), não em blocos de 24 horas: cada faixa começa no início de um dia local, convertido para UTC
        // pelo LocalDatePeriod, como os períodos de abertura do recorte.
        DateOnly today = LocalDatePeriod.ToLocalDate(nowUtc);

        IEnumerable<TicketOverviewResponse> overviews = await connection.QueryAsync<
            TicketOverviewResponse,
            TicketOverviewVolumeResponse,
            TicketOverviewSlaResponse,
            TicketOverviewAttentionResponse,
            TicketOverviewAgingResponse,
            TicketOverviewResponse>(
            new CommandDefinition(
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
                    NowUtc = nowUtc,
                    OneDayAgoStartUtc = StartOfLocalDayUtc(today.AddDays(-1)),
                    ThreeDaysAgoStartUtc = StartOfLocalDayUtc(today.AddDays(-3)),
                    SevenDaysAgoStartUtc = StartOfLocalDayUtc(today.AddDays(-7))
                },
                cancellationToken: cancellationToken),
            (response, volume, sla, attention, aging) =>
            {
                response.Volume = volume;
                response.Sla = sla;
                response.Attention = attention;
                response.Aging = aging;

                return response;
            },
            splitOn);

        return overviews.Single();
    }

    private static DateTime? StartOfLocalDayUtc(DateOnly day) => LocalDatePeriod.FromLocalDates(day, null).FromUtc;
}
