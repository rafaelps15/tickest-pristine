using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetOverview;

/// <summary>
/// Conta tudo num só comando, com contagens condicionais sobre os chamados do recorte (spec 026, DEC-2). As condições de
/// SLA repetem as de <see cref="TicketSlaFilters"/> (relógio correndo = sem resolução e não cancelado; no prazo, em
/// risco e atrasado pelo instante atual; cumprido e perdido pela primeira resolução contra o prazo; sem SLA = cancelado
/// nunca resolvido), e os testes de paridade com a listagem conferem que as duas versões concordam.
/// </summary>
internal sealed class GetTicketOverviewQueryHandler(IDbConnectionFactory dbConnectionFactory, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTicketOverviewQuery, TicketOverviewResponse>
{
    private const string Open = nameof(TicketStatus.Open);
    private const string InProgress = nameof(TicketStatus.InProgress);
    private const string Resolved = nameof(TicketStatus.Resolved);
    private const string Closed = nameof(TicketStatus.Closed);
    private const string Canceled = nameof(TicketStatus.Canceled);
    private const string Urgent = nameof(TicketPriority.Urgent);

    private const string Running = $"t.resolved_at_utc IS NULL AND t.status <> '{Canceled}'";
    private const string Active = $"t.status IN ('{Open}', '{InProgress}')";

    private const string Sql =
        $"""
         SELECT
             COUNT(*)::int AS {nameof(OverviewCounts.TotalCount)},
             COUNT(*) FILTER (WHERE t.status = '{Open}')::int AS {nameof(OverviewCounts.OpenCount)},
             COUNT(*) FILTER (WHERE t.status = '{InProgress}')::int AS {nameof(OverviewCounts.InProgressCount)},
             COUNT(*) FILTER (WHERE t.status = '{Resolved}')::int AS {nameof(OverviewCounts.ResolvedCount)},
             COUNT(*) FILTER (WHERE t.status = '{Closed}')::int AS {nameof(OverviewCounts.ClosedCount)},
             COUNT(*) FILTER (WHERE t.status = '{Canceled}')::int AS {nameof(OverviewCounts.CanceledCount)},
             COUNT(*) FILTER (WHERE {Running} AND @NowUtc::timestamptz < t.sla_warning_at_utc)::int AS {nameof(OverviewCounts.OnTimeCount)},
             COUNT(*) FILTER (WHERE {Running} AND @NowUtc::timestamptz >= t.sla_warning_at_utc AND @NowUtc::timestamptz < t.due_at_utc)::int AS {nameof(OverviewCounts.AtRiskCount)},
             COUNT(*) FILTER (WHERE {Running} AND @NowUtc::timestamptz >= t.due_at_utc)::int AS {nameof(OverviewCounts.BreachedCount)},
             COUNT(*) FILTER (WHERE t.resolved_at_utc IS NOT NULL AND t.resolved_at_utc <= t.due_at_utc)::int AS {nameof(OverviewCounts.MetCount)},
             COUNT(*) FILTER (WHERE t.resolved_at_utc IS NOT NULL AND t.resolved_at_utc > t.due_at_utc)::int AS {nameof(OverviewCounts.MissedCount)},
             COUNT(*) FILTER (WHERE t.resolved_at_utc IS NULL AND t.status = '{Canceled}')::int AS {nameof(OverviewCounts.NotApplicableCount)},
             COUNT(*) FILTER (WHERE {Active} AND t.priority = '{Urgent}')::int AS {nameof(OverviewCounts.CriticalCount)},
             COUNT(*) FILTER (WHERE {Active} AND t.assigned_to_user_id IS NULL)::int AS {nameof(OverviewCounts.UnassignedCount)}
         FROM public.tickets t
         WHERE {TicketScopeFilter.SqlCondition}
         """;

    public async Task<Result<TicketOverviewResponse>> Handle(GetTicketOverviewQuery query, CancellationToken cancellationToken)
    {
        if (query.Scope.HasInvertedPeriod)
        {
            return Result.Failure<TicketOverviewResponse>(TicketErrors.InvalidPeriod);
        }

        DateTime nowUtc = dateTimeProvider.UtcNow;
        DynamicParameters parameters = query.Scope.ToSqlParameters();
        parameters.Add("NowUtc", nowUtc);

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        OverviewCounts counts = await connection.QuerySingleAsync<OverviewCounts>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken));

        return new TicketOverviewResponse
        {
            ServerNowUtc = nowUtc,
            Volume = new TicketOverviewVolumeResponse
            {
                TotalCount = counts.TotalCount,
                OpenCount = counts.OpenCount,
                InProgressCount = counts.InProgressCount,
                ResolvedCount = counts.ResolvedCount,
                ClosedCount = counts.ClosedCount,
                CanceledCount = counts.CanceledCount,
                BacklogCount = counts.OpenCount + counts.InProgressCount
            },
            Sla = new TicketOverviewSlaResponse
            {
                OnTimeCount = counts.OnTimeCount,
                AtRiskCount = counts.AtRiskCount,
                BreachedCount = counts.BreachedCount,
                MetCount = counts.MetCount,
                MissedCount = counts.MissedCount,
                NotApplicableCount = counts.NotApplicableCount
            },
            Attention = new TicketOverviewAttentionResponse
            {
                BreachedCount = counts.BreachedCount,
                CriticalCount = counts.CriticalCount,
                UnassignedCount = counts.UnassignedCount
            }
        };
    }

    /// <summary>
    /// Linha única devolvida pelo SQL, lida pelo Dapper pelos nomes das colunas.
    /// </summary>
    internal sealed class OverviewCounts
    {
        public int TotalCount { get; init; }
        public int OpenCount { get; init; }
        public int InProgressCount { get; init; }
        public int ResolvedCount { get; init; }
        public int ClosedCount { get; init; }
        public int CanceledCount { get; init; }
        public int OnTimeCount { get; init; }
        public int AtRiskCount { get; init; }
        public int BreachedCount { get; init; }
        public int MetCount { get; init; }
        public int MissedCount { get; init; }
        public int NotApplicableCount { get; init; }
        public int CriticalCount { get; init; }
        public int UnassignedCount { get; init; }
    }
}
