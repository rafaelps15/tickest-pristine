using System.Data.Common;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Tickets.GetAssignees;

/// <summary>
/// Lista os responsáveis elegíveis pela mesma regra da atribuição (<see cref="UserSql.IsTicketAgent"/>), para a lista e
/// a atribuição nunca discordarem. O endpoint exige <c>tickets:manage</c>.
/// </summary>
internal sealed class GetTicketAssigneesQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetTicketAssigneesQuery, PagedResponse<TicketAssigneeResponse>>
{
    // Palavras do termo de busca que entram no filtro; as demais são ignoradas, como na busca de atendentes.
    private const int SearchTermMaxWords = 5;

    public async Task<Result<PagedResponse<TicketAssigneeResponse>>> Handle(
        GetTicketAssigneesQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // Cada palavra precisa estar no nome ou no sobrenome. As colunas são citext, então o LIKE não diferencia
        // maiúsculas, e cada palavra vai como padrão escapado (%, _ e \ contam como texto).
        const string filter =
            $"""
             {UserSql.IsTicketAgent}
               AND (@SectorId::uuid IS NULL OR u.sector_id = @SectorId::uuid)
               AND NOT EXISTS (
                   SELECT 1
                   FROM unnest(@SearchPatterns::text[]) AS w(pattern)
                   WHERE NOT (u.first_name LIKE w.pattern OR u.last_name LIKE w.pattern))
             """;

        // O setor é opcional: a junção à esquerda mantém quem não tem setor, e aí o nome vem nulo.
        const string sql =
            $"""
             SELECT COUNT(*)::int
             FROM public.users u
             LEFT JOIN public.sectors s ON s.id = u.sector_id
             WHERE {filter};

             SELECT
                 u.id AS {nameof(TicketAssigneeResponse.Id)},
                 u.first_name || ' ' || u.last_name AS {nameof(TicketAssigneeResponse.FullName)},
                 u.sector_id AS {nameof(TicketAssigneeResponse.SectorId)},
                 s.name AS {nameof(TicketAssigneeResponse.SectorName)}
             FROM public.users u
             LEFT JOIN public.sectors s ON s.id = u.sector_id
             WHERE {filter}
             ORDER BY u.first_name, u.last_name, u.id
             LIMIT @Limit OFFSET @Offset
             """;

        PagedResponse<TicketAssigneeResponse> assignees = await connection.QueryPagedAsync<TicketAssigneeResponse>(
            sql,
            new { request.SectorId, SearchPatterns = SqlLikePattern.ForWords(request.Search, SearchTermMaxWords) },
            request.Paging,
            cancellationToken);

        return assignees;
    }
}
