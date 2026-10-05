using System.Security.Claims;
using TickestPristine.Domain.Users;

namespace TickestPristine.Infrastructure.Authentication;

/// <summary>
/// Claims de identificação do usuário. As permissões não vão no token: a API as resolve no servidor a cada requisição
/// e os clientes as leem em GET /users/me.
/// </summary>
internal sealed class ClaimsProvider : IClaimsProvider
{
    public Task<Claim[]> GetClaimsAsync(User user, CancellationToken cancellationToken = default)
    {
        Claim[] claims =
        [
            new(TokenClaimTypes.UserId, user.Id.ToString()),
            new(TokenClaimTypes.Email, user.Email),
            new(TokenClaimTypes.Name, $"{user.FirstName} {user.LastName}")
        ];

        return Task.FromResult(claims);
    }
}
