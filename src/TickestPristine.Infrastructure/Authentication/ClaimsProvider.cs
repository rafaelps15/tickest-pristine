using System.Security.Claims;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Domain.Users;

namespace TickestPristine.Infrastructure.Authentication;

internal sealed class ClaimsProvider(IPermissionProvider permissionProvider) : IClaimsProvider
{
    public async Task<Claim[]> GetClaimsAsync(User user, CancellationToken cancellationToken = default)
    {
        var claims = new List<Claim>
        {
            new(TokenClaimTypes.UserId, user.Id.ToString()),
            new(TokenClaimTypes.Email, user.Email),
            new(TokenClaimTypes.Name, $"{user.FirstName} {user.LastName}")
        };

        HashSet<string> permissions = await permissionProvider.GetForUserIdAsync(user.Id, cancellationToken);

        claims.AddRange(permissions.Select(permission => new Claim(TokenClaimTypes.Permissions, permission)));

        return [.. claims];
    }
}
