using System.Data.Common;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.GetAttendants;

/// <summary>
/// Lista quem atende chamados (<see cref="UserSql.IsTicketAgent"/>). O endpoint exige só usuário ativo: a permissão
/// <c>users:read</c> é conferida aqui, para a falta dela responder 403 com mensagem de negócio.
/// </summary>
internal sealed class GetBacklogAttendantsQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetBacklogAttendantsQuery, PagedResponse<AttendantResponse>>
{
    // Tamanho aceito do termo de busca, depois do Trim.
    private const int SearchTermMinLength = 2;
    private const int SearchTermMaxLength = 100;

    // Palavras do termo de busca que entram no filtro; as demais são ignoradas.
    private const int SearchTermMaxWords = 5;

    public async Task<Result<PagedResponse<AttendantResponse>>> Handle(
        GetBacklogAttendantsQuery request,
        CancellationToken cancellationToken)
    {
        // Termo vazio conta como ausente; só com espaços, não.
        if (!string.IsNullOrEmpty(request.SearchTerm) &&
            request.SearchTerm.Trim().Length is < SearchTermMinLength or > SearchTermMaxLength)
        {
            return Result.Failure<PagedResponse<AttendantResponse>>(UserErrors.InvalidSearchTerm(
                SearchTermMinLength,
                SearchTermMaxLength));
        }

        bool canReadUsers = await permissionProvider.HasPermissionAsync(
            userContext.UserId,
            PermissionCodes.Users.Read,
            cancellationToken);

        if (!canReadUsers)
        {
            return Result.Failure<PagedResponse<AttendantResponse>>(UserErrors.ReadForbidden);
        }

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // Cada palavra precisa estar em algum dos campos. As colunas são citext, então o LIKE não diferencia maiúsculas, e
        // cada palavra vai como padrão escapado (%, _ e \ contam como texto); um nome de setor ou departamento nulo não
        // atende à palavra.
        const string filter =
            $"""
             {UserSql.IsTicketAgent}
               AND (@SectorId::uuid IS NULL OR u.sector_id = @SectorId::uuid)
               AND NOT EXISTS (
                   SELECT 1
                   FROM unnest(@SearchPatterns::text[]) AS w(pattern)
                   WHERE NOT (u.first_name LIKE w.pattern
                              OR u.last_name LIKE w.pattern
                              OR u.email LIKE w.pattern
                              OR (s.name IS NOT NULL AND s.name LIKE w.pattern)
                              OR (d.name IS NOT NULL AND d.name LIKE w.pattern)))
             """;

        // Setor e departamento são opcionais: as junções à esquerda mantêm quem não tem setor, e aí os nomes vêm nulos.
        const string sql =
            $"""
             SELECT COUNT(*)::int
             FROM public.users u
             LEFT JOIN public.sectors s ON s.id = u.sector_id
             LEFT JOIN public.departments d ON d.id = s.department_id
             WHERE {filter};

             SELECT
                 u.id AS {nameof(AttendantResponse.Id)},
                 u.first_name || ' ' || u.last_name AS {nameof(AttendantResponse.FullName)},
                 u.email AS {nameof(AttendantResponse.Email)},
                 u.sector_id AS {nameof(AttendantResponse.SectorId)},
                 s.name AS {nameof(AttendantResponse.SectorName)},
                 d.name AS {nameof(AttendantResponse.DepartmentName)}
             FROM public.users u
             LEFT JOIN public.sectors s ON s.id = u.sector_id
             LEFT JOIN public.departments d ON d.id = s.department_id
             WHERE {filter}
             ORDER BY CASE WHEN u.sector_id = @PrioritySectorId::uuid THEN 0 ELSE 1 END, u.first_name, u.last_name, u.id
             LIMIT @Limit OFFSET @Offset
             """;

        PagedResponse<AttendantResponse> attendants = await connection.QueryPagedAsync<AttendantResponse>(
            sql,
            new
            {
                request.SectorId,
                request.PrioritySectorId,
                SearchPatterns = SqlLikePattern.ForWords(request.SearchTerm, SearchTermMaxWords)
            },
            request.Paging,
            cancellationToken);

        return attendants;
    }
}
