using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.GetCurrent;

internal sealed class GetCurrentUserQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    public async Task<Result<CurrentUserResponse>> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // Mesma regra do login e do refresh: o access token ainda vale, mas a conta não pode mais ser usada.
        if (!await HasPermissionToUseAccount(connection, cancellationToken))
        {
            return Result.Failure<CurrentUserResponse>(UserErrors.Deactivated);
        }

        // Uma linha por função do usuário, em ordem de nome; sem função, a linha vem sem ela.
        const string sql =
            $"""
             SELECT
                 u.id AS {nameof(CurrentUserResponse.Id)},
                 u.email AS {nameof(CurrentUserResponse.Email)},
                 u.first_name AS {nameof(CurrentUserResponse.FirstName)},
                 u.last_name AS {nameof(CurrentUserResponse.LastName)},
                 ur.role_name AS {nameof(CurrentUserRoleResponse.Name)}
             FROM public.users u
             LEFT JOIN public.user_roles ur ON ur.user_id = u.id
             WHERE u.id = @UserId
             ORDER BY ur.role_name
             """;

        var users = new List<CurrentUserResponse>();

        await connection.QueryAsync<CurrentUserResponse, CurrentUserRoleResponse?, CurrentUserResponse>(
            new CommandDefinition(sql, new { userContext.UserId }, cancellationToken: cancellationToken),
            (user, role) =>
            {
                if (users.Count == 0)
                {
                    users.Add(user);
                }

                if (role is not null)
                {
                    users[0].Roles.Add(role);
                }

                return user;
            },
            splitOn: nameof(CurrentUserRoleResponse.Name));

        if (users.Count == 0)
        {
            return Result.Failure<CurrentUserResponse>(UserErrors.NotFound(userContext.UserId));
        }

        // As permissões não saem do SQL: vêm da mesma fonte que a autorização usa, para o front mostrar exatamente o que a
        // API vai permitir.
        HashSet<string> permissions = await permissionProvider.GetForUserIdAsync(userContext.UserId, cancellationToken);

        CurrentUserResponse currentUser = users[0];
        currentUser.Permissions.AddRange(permissions.Order(StringComparer.Ordinal));

        return currentUser;
    }

    /// <summary>
    /// Falso só quando o usuário atual existe e está com a conta desativada; para um usuário que não existe, devolve
    /// verdadeiro, e o NotFound sai da leitura principal.
    /// </summary>
    private async Task<bool> HasPermissionToUseAccount(DbConnection connection, CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT NOT EXISTS (
                SELECT 1
                FROM public.users u
                WHERE u.id = @UserId AND NOT u.is_active)
            """;

        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { userContext.UserId }, cancellationToken: cancellationToken));
    }
}
