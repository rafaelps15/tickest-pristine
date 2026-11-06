using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.GetById;

internal sealed class GetUserByIdQueryHandler(
    IDbConnectionFactory dbConnectionFactory,
    IUserContext userContext,
    IPermissionProvider permissionProvider,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetUserByIdQuery, UserResponse>
{
    public async Task<Result<UserResponse>> Handle(
        GetUserByIdQuery request,
        CancellationToken cancellationToken)
    {
        bool canReadUsers = await permissionProvider.IsSelfOrHasPermissionAsync(
            userContext.UserId,
            request.UserId,
            PermissionCodes.Users.Read,
            cancellationToken);

        if (!canReadUsers)
        {
            return Result.Failure<UserResponse>(UserErrors.Unauthorized());
        }

        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // Setor opcional: sem setor, as junções à esquerda trazem os campos nulos; o setor desativado continua
        // aparecendo, com SectorIsActive = false.
        const string sql =
            $"""
             SELECT
                 u.id AS {nameof(UserResponse.Id)},
                 u.first_name AS {nameof(UserResponse.FirstName)},
                 u.last_name AS {nameof(UserResponse.LastName)},
                 u.email AS {nameof(UserResponse.Email)},
                 u.sector_id AS {nameof(UserResponse.SectorId)},
                 s.name AS {nameof(UserResponse.SectorName)},
                 d.name AS {nameof(UserResponse.DepartmentName)},
                 s.is_active AS {nameof(UserResponse.SectorIsActive)},
                 {UserSql.IsPendingActivation} AS {nameof(UserResponse.PendingActivation)},
                 {UserSql.PendingInvitationExpiresAtUtc} AS {nameof(UserResponse.InvitationExpiresAtUtc)}
             FROM public.users u
             LEFT JOIN public.sectors s ON s.id = u.sector_id
             LEFT JOIN public.departments d ON d.id = s.department_id
             WHERE u.id = @UserId
             """;

        UserResponse? user = await connection.QuerySingleOrDefaultAsync<UserResponse>(
            new CommandDefinition(
                sql,
                new { request.UserId, NowUtc = dateTimeProvider.UtcNow },
                cancellationToken: cancellationToken));

        if (user is null)
        {
            return Result.Failure<UserResponse>(UserErrors.NotFound(request.UserId));
        }

        return user;
    }
}
