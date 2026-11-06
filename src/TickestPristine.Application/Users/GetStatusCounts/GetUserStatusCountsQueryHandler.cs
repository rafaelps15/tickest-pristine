using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.GetStatusCounts;

internal sealed class GetUserStatusCountsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetUserStatusCountsQuery, UserStatusCountsResponse>
{
    public async Task<Result<UserStatusCountsResponse>> Handle(
        GetUserStatusCountsQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // Um comando só: o banco agrupa por situação e "pendente" (no máximo três grupos) e soma os grupos nas quatro
        // contagens. Conta pendente é o usuário ativo sem senha, o mesmo trecho da listagem e do detalhe; ativo conta só
        // quem não está pendente, e inativo é a conta desativada. Sem usuários, as contagens saem zeradas.
        const string sql =
            $"""
             SELECT
                 COALESCE(SUM(g.total), 0)::int AS {nameof(UserStatusCountsResponse.TotalCount)},
                 COALESCE(SUM(g.total) FILTER (WHERE g.is_active AND NOT g.is_pending_activation), 0)::int AS {nameof(UserStatusCountsResponse.ActiveCount)},
                 COALESCE(SUM(g.total) FILTER (WHERE g.is_pending_activation), 0)::int AS {nameof(UserStatusCountsResponse.PendingActivationCount)},
                 COALESCE(SUM(g.total) FILTER (WHERE NOT g.is_active), 0)::int AS {nameof(UserStatusCountsResponse.InactiveCount)}
             FROM (
                 SELECT u.is_active, ({UserSql.IsPendingActivation}) AS is_pending_activation, COUNT(*) AS total
                 FROM public.users u
                 GROUP BY 1, 2) g
             """;

        UserStatusCountsResponse counts = await connection.QuerySingleAsync<UserStatusCountsResponse>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return counts;
    }
}
