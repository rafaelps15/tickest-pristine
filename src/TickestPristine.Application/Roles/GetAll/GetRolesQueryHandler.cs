using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Roles.GetAll;

internal sealed class GetRolesQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetRolesQuery, List<RoleResponse>>
{
    public async Task<Result<List<RoleResponse>>> Handle(
        GetRolesQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string permissionCodeColumn = "PermissionCode";

        // Uma linha por permissão da role, com a role repetida; sem permissão, a linha vem sem código.
        const string sql =
            $"""
             SELECT
                 r.name AS {nameof(RoleResponse.Name)},
                 r.name = '{RoleNames.Collaborator}' AS {nameof(RoleResponse.IsDefault)},
                 r.name = '{RoleNames.Administrator}' AS {nameof(RoleResponse.IsAdministrator)},
                 rp.permission_code AS {permissionCodeColumn}
             FROM public.roles r
             LEFT JOIN public.role_permissions rp ON rp.role_name = r.name
             ORDER BY r.name, r.id
             """;

        var roles = new List<RoleResponse>();

        await connection.QueryAsync<RoleResponse, string?, RoleResponse>(
            new CommandDefinition(sql, cancellationToken: cancellationToken),
            (role, permissionCode) =>
            {
                if (roles.Count == 0 || roles[^1].Name != role.Name)
                {
                    roles.Add(role);
                }

                if (permissionCode is not null)
                {
                    roles[^1].PermissionCodes.Add(permissionCode);
                }

                return role;
            },
            splitOn: permissionCodeColumn);

        return roles;
    }
}
