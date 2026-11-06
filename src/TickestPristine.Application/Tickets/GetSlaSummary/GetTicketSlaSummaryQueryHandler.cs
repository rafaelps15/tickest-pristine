using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetSlaSummary;

internal sealed class GetTicketSlaSummaryQueryHandler(IDbConnectionFactory dbConnectionFactory, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTicketSlaSummaryQuery, TicketSlaSummaryResponse>
{
    public async Task<Result<TicketSlaSummaryResponse>> Handle(
        GetTicketSlaSummaryQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // Uma consulta só: as quatro contagens e o instante usado nelas saem do mesmo comando, sem um chamado mudar entre
        // uma e outra. Sem nenhum chamado ativo, as contagens saem zeradas.
        const string sql =
            $"""
             SELECT
                 COUNT(*)::int AS {nameof(TicketSlaSummaryResponse.ActiveCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsSlaOnTime})::int AS {nameof(TicketSlaSummaryResponse.OnTimeCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsSlaAtRisk})::int AS {nameof(TicketSlaSummaryResponse.AtRiskCount)},
                 COUNT(*) FILTER (WHERE {TicketSql.IsSlaBreached})::int AS {nameof(TicketSlaSummaryResponse.BreachedCount)},
                 @NowUtc::timestamptz AS {nameof(TicketSlaSummaryResponse.ServerNowUtc)}
             FROM public.tickets t
             WHERE t.deleted_at_utc IS NULL
               AND {TicketSql.IsActive}
               AND (@SectorId::uuid IS NULL OR t.sector_id = @SectorId::uuid)
             """;

        TicketSlaSummaryResponse summary = await connection.QuerySingleAsync<TicketSlaSummaryResponse>(
            new CommandDefinition(
                sql,
                new { request.SectorId, NowUtc = dateTimeProvider.UtcNow },
                cancellationToken: cancellationToken));

        return summary;
    }
}
