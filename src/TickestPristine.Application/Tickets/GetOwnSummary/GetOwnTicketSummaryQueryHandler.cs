using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetOwnSummary;

internal sealed class GetOwnTicketSummaryQueryHandler(IDbConnectionFactory dbConnectionFactory, IUserContext userContext)
    : IQueryHandler<GetOwnTicketSummaryQuery, List<OwnTicketCountResponse>>
{
    public async Task<Result<List<OwnTicketCountResponse>>> Handle(
        GetOwnTicketSummaryQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            $"""
             SELECT
                 t.priority AS {nameof(OwnTicketCountResponse.Priority)},
                 t.status AS {nameof(OwnTicketCountResponse.Status)},
                 COUNT(*)::int AS {nameof(OwnTicketCountResponse.Count)}
             FROM public.tickets t
             WHERE t.deleted_at_utc IS NULL AND t.created_by_user_id = @UserId
             GROUP BY t.priority, t.status
             """;

        List<OwnTicketCountResponse> counts = (await connection.QueryAsync<OwnTicketCountResponse>(
            new CommandDefinition(sql, new { userContext.UserId }, cancellationToken: cancellationToken))).AsList();

        // Ordenado em memória: no banco os enums são texto e a ordem seria alfabética, não a de gravidade.
        return counts.OrderBy(c => c.Priority).ThenBy(c => c.Status).ToList();
    }
}
