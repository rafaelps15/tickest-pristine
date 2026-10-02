using System.Security.Claims;
using TickestPristine.Domain.Users;

namespace TickestPristine.Infrastructure.Authentication;

/// <summary>
/// Monta as claims do usuário gravadas no JWT.
/// </summary>
internal interface IClaimsProvider
{
    Task<Claim[]> GetClaimsAsync(User user, CancellationToken cancellationToken = default);
}
