using Microsoft.IdentityModel.JsonWebTokens;

namespace TickestPristine.Infrastructure.Authentication;

/// <summary>
/// Tipos de claim gravados no JWT.
/// </summary>
internal static class TokenClaimTypes
{
    internal const string UserId = JwtRegisteredClaimNames.Sub;

    internal const string Email = JwtRegisteredClaimNames.Email;

    internal const string Name = JwtRegisteredClaimNames.Name;

    internal const string Permissions = "permissions";
}
