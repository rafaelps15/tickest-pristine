using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetVolume;

internal sealed class GetTicketVolumeQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetTicketVolumeQuery, List<TicketVolumeResponse>>
{
    public async Task<Result<List<TicketVolumeResponse>>> Handle(GetTicketVolumeQuery request, CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        // @GroupBy chega como o número do TicketVolumeGrouping (0 = dia, 1 = semana, 2 = mês). Os períodos seguem o
        // horário de Brasília e a semana do date_trunc começa na segunda-feira. Chamados excluídos ficam de fora, como
        // no filtro global do EF.
        const string sql =
            $"""
             WITH settings AS (
                 SELECT
                     CASE @GroupBy WHEN 1 THEN 'week' WHEN 2 THEN 'month' ELSE 'day' END AS unit,
                     CASE @GroupBy WHEN 1 THEN interval '7 days' WHEN 2 THEN interval '1 month' ELSE interval '1 day' END AS step,
                     CASE @GroupBy WHEN 1 THEN 12 WHEN 2 THEN 12 ELSE 30 END AS points,
                     now() AT TIME ZONE 'America/Sao_Paulo' AS local_now
             ),
             periods AS (
                 SELECT generate_series(
                     date_trunc(s.unit, s.local_now) - (s.points - 1) * s.step,
                     date_trunc(s.unit, s.local_now),
                     s.step) AS period_start
                 FROM settings s
             ),
             tickets_in_window AS (
                 SELECT date_trunc(s.unit, t.created_at_utc AT TIME ZONE 'America/Sao_Paulo') AS period_start
                 FROM public.tickets t
                 CROSS JOIN settings s
                 WHERE t.deleted_at_utc IS NULL
                   AND t.created_at_utc >= (SELECT MIN(period_start) FROM periods) AT TIME ZONE 'America/Sao_Paulo'
             )
             SELECT
                 p.period_start::date AS {nameof(TicketVolumeResponse.PeriodStart)},
                 COUNT(t.period_start)::int AS {nameof(TicketVolumeResponse.Count)}
             FROM periods p
             LEFT JOIN tickets_in_window t ON t.period_start = p.period_start
             GROUP BY p.period_start
             ORDER BY p.period_start
             """;

        IEnumerable<TicketVolumeResponse> volume = await connection.QueryAsync<TicketVolumeResponse>(sql, request);

        return volume.AsList();
    }
}
