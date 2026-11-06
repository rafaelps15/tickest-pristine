using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.GetByEmail;

internal sealed class GetUserByEmailQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetUserByEmailQuery, UserResponse>
{
    public async Task<Result<UserResponse>> Handle(
        GetUserByEmailQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // O e-mail é citext: o cast do parâmetro faz a comparação não diferenciar maiúsculas, como na coluna.
        const string sql =
            $"""
             SELECT
                 id AS {nameof(UserResponse.Id)},
                 first_name AS {nameof(UserResponse.FirstName)},
                 last_name AS {nameof(UserResponse.LastName)},
                 email AS {nameof(UserResponse.Email)}
             FROM public.users
             WHERE email = @Email::citext
             """;

        UserResponse? user = await connection.QuerySingleOrDefaultAsync<UserResponse>(
            new CommandDefinition(sql, request, cancellationToken: cancellationToken));

        if (user is null)
        {
            return Result.Failure<UserResponse>(UserErrors.NotFoundByEmail);
        }

        return user;
    }
}
