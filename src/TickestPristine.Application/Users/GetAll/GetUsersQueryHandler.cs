using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.GetAll;

internal sealed class GetUsersQueryHandler(IDbConnectionFactory dbConnectionFactory, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetUsersQuery, PagedResponse<UserSummaryResponse>>
{
    public async Task<Result<PagedResponse<UserSummaryResponse>>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // As colunas de nome e de e-mail são citext, então o LIKE não diferencia maiúsculas.
        const string filter =
            """
            (@IsActive::boolean IS NULL OR u.is_active = @IsActive::boolean)
              AND (@SearchPattern::text IS NULL
                   OR u.first_name LIKE @SearchPattern::text
                   OR u.last_name LIKE @SearchPattern::text
                   OR u.email LIKE @SearchPattern::text)
              AND (@CreatedFromUtc::timestamptz IS NULL OR u.created_at_utc >= @CreatedFromUtc::timestamptz)
              AND (@CreatedUntilUtc::timestamptz IS NULL OR u.created_at_utc < @CreatedUntilUtc::timestamptz)
              AND (NOT @WithoutRole OR NOT EXISTS (SELECT 1 FROM public.user_roles ur WHERE ur.user_id = u.id))
              AND (@RoleName::text IS NULL
                   OR EXISTS (SELECT 1 FROM public.user_roles ur WHERE ur.user_id = u.id AND ur.role_name = @RoleName::text))
            """;

        // Setor opcional: sem setor, as junções à esquerda trazem os campos nulos; o setor desativado continua aparecendo,
        // com SectorIsActive = false.
        const string countAndPage =
            $"""
             SELECT COUNT(*)::int
             FROM public.users u
             LEFT JOIN public.sectors s ON s.id = u.sector_id
             LEFT JOIN public.departments d ON d.id = s.department_id
             WHERE {filter};

             SELECT
                 u.id AS {nameof(UserSummaryResponse.Id)},
                 u.email AS {nameof(UserSummaryResponse.Email)},
                 u.first_name AS {nameof(UserSummaryResponse.FirstName)},
                 u.last_name AS {nameof(UserSummaryResponse.LastName)},
                 u.is_active AS {nameof(UserSummaryResponse.IsActive)},
                 u.created_at_utc AS {nameof(UserSummaryResponse.CreatedAtUtc)},
                 u.deactivated_at_utc AS {nameof(UserSummaryResponse.DeactivatedAtUtc)},
                 u.sector_id AS {nameof(UserSummaryResponse.SectorId)},
                 s.name AS {nameof(UserSummaryResponse.SectorName)},
                 d.name AS {nameof(UserSummaryResponse.DepartmentName)},
                 s.is_active AS {nameof(UserSummaryResponse.SectorIsActive)},
                 {UserSql.IsPendingActivation} AS {nameof(UserSummaryResponse.PendingActivation)},
                 {UserSql.PendingInvitationExpiresAtUtc} AS {nameof(UserSummaryResponse.InvitationExpiresAtUtc)}
             FROM public.users u
             LEFT JOIN public.sectors s ON s.id = u.sector_id
             LEFT JOIN public.departments d ON d.id = s.department_id
             WHERE {filter}
             """;

        // Uma constante por ordenação; o Id desempata as chaves iguais, para a paginação não repetir nem pular usuários,
        // e a direção vale para todas as chaves.
        const string nameAscendingSql =
            $"""
             {countAndPage}
             ORDER BY u.first_name, u.last_name, u.id
             LIMIT @Limit OFFSET @Offset
             """;

        const string nameDescendingSql =
            $"""
             {countAndPage}
             ORDER BY u.first_name DESC, u.last_name DESC, u.id DESC
             LIMIT @Limit OFFSET @Offset
             """;

        const string oldestFirstSql =
            $"""
             {countAndPage}
             ORDER BY u.created_at_utc, u.id
             LIMIT @Limit OFFSET @Offset
             """;

        const string newestFirstSql =
            $"""
             {countAndPage}
             ORDER BY u.created_at_utc DESC, u.id DESC
             LIMIT @Limit OFFSET @Offset
             """;

        // Sem direção, o nome é crescente e o cadastro, decrescente; só a direção crescente inverte o cadastro, e só a
        // falta de direção ou a crescente mantém o nome crescente.
        string sql = (request.SortBy ?? UserSortField.Name, request.SortDirection) switch
        {
            (UserSortField.CreatedAt, SortDirection.Ascending) => oldestFirstSql,
            (UserSortField.CreatedAt, _) => newestFirstSql,
            (_, null or SortDirection.Ascending) => nameAscendingSql,
            _ => nameDescendingSql
        };

        string? search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        string? roleName = request.WithoutRole || string.IsNullOrWhiteSpace(request.RoleName) ? null : request.RoleName.Trim();

        PagedResponse<UserSummaryResponse> users = await connection.QueryPagedAsync<UserSummaryResponse>(
            sql,
            new
            {
                IsActive = request.Status == UserStatusFilter.All ? (bool?)null : request.Status == UserStatusFilter.Active,
                SearchPattern = search is null ? null : SqlLikePattern.Containing(search),
                CreatedFromUtc = request.CreatedFrom?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                CreatedUntilUtc = request.CreatedTo?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                request.WithoutRole,
                RoleName = roleName,
                NowUtc = dateTimeProvider.UtcNow
            },
            request.Paging,
            cancellationToken);

        if (users.Items.Count == 0)
        {
            return users;
        }

        const string userIdColumn = "UserId";

        // As roles da página numa segunda leitura, com o usuário de cada uma ao lado, e cada uma vai para a lista dele.
        const string rolesSql =
            $"""
             SELECT
                 ur.role_name AS {nameof(RoleSummaryResponse.Name)},
                 ur.user_id AS {userIdColumn}
             FROM public.user_roles ur
             WHERE ur.user_id = ANY(@UserIds)
             """;

        IEnumerable<(Guid UserId, RoleSummaryResponse Role)> roles =
            await connection.QueryAsync<RoleSummaryResponse, Guid, (Guid UserId, RoleSummaryResponse Role)>(
                new CommandDefinition(
                    rolesSql,
                    new { UserIds = users.Items.Select(user => user.Id).ToArray() },
                    cancellationToken: cancellationToken),
                (role, userId) => (userId, role),
                splitOn: userIdColumn);

        ILookup<Guid, RoleSummaryResponse> rolesByUser = roles.ToLookup(entry => entry.UserId, entry => entry.Role);

        foreach (UserSummaryResponse user in users.Items)
        {
            user.Roles.AddRange(rolesByUser[user.Id]);
        }

        return users;
    }
}
