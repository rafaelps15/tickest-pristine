using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Permissions.GetAll;

internal sealed class GetPermissionsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetPermissionsQuery, IReadOnlyCollection<PermissionResponse>>
{
    public async Task<Result<IReadOnlyCollection<PermissionResponse>>> Handle(
        GetPermissionsQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            $"""
             SELECT
                 code AS {nameof(PermissionResponse.Code)},
                 name AS {nameof(PermissionResponse.Name)},
                 description AS {nameof(PermissionResponse.Description)},
                 "group" AS {nameof(PermissionResponse.Group)},
                 is_administrative AS {nameof(PermissionResponse.IsAdministrative)}
             FROM public.permissions
             ORDER BY code
             """;

        List<PermissionResponse> permissions = (await connection.QueryAsync<PermissionResponse>(
            new CommandDefinition(sql, request, cancellationToken: cancellationToken))).AsList();

        return permissions;
    }
}
