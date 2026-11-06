using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetStatusCounts;

internal sealed class GetTicketStatusCountsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetTicketStatusCountsQuery, TicketStatusCountsResponse>
{
    public async Task<Result<TicketStatusCountsResponse>> Handle(
        GetTicketStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            $"""
             SELECT
                 COUNT(*)::int AS {nameof(TicketStatusCountsResponse.TotalCount)},
                 COUNT(*) FILTER (WHERE t.status = '{nameof(TicketStatus.Open)}')::int AS {nameof(TicketStatusCountsResponse.OpenCount)},
                 COUNT(*) FILTER (WHERE t.status = '{nameof(TicketStatus.InProgress)}')::int AS {nameof(TicketStatusCountsResponse.InProgressCount)},
                 COUNT(*) FILTER (WHERE t.status = '{nameof(TicketStatus.Resolved)}')::int AS {nameof(TicketStatusCountsResponse.ResolvedCount)},
                 COUNT(*) FILTER (WHERE t.status = '{nameof(TicketStatus.Closed)}')::int AS {nameof(TicketStatusCountsResponse.ClosedCount)},
                 COUNT(*) FILTER (WHERE t.status = '{nameof(TicketStatus.Canceled)}')::int AS {nameof(TicketStatusCountsResponse.CanceledCount)}
             FROM public.tickets t
             WHERE t.deleted_at_utc IS NULL
             """;

        TicketStatusCountsResponse counts = await connection.QuerySingleAsync<TicketStatusCountsResponse>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return counts;
    }
}
