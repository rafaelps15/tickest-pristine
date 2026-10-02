using System.Security.Claims;
using System.Security.Cryptography;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TickestPristine.SharedKernel;

namespace TickestPristine.Infrastructure.Authentication;

internal sealed class TokenProvider(
    IOptions<JwtOptions> jwtOptions,
    IClaimsProvider claimsProvider,
    IDateTimeProvider dateTimeProvider) : ITokenProvider
{
    public async Task<string> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        JwtOptions jwt = jwtOptions.Value;

        var credentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256);

        Claim[] claims = await claimsProvider.GetClaimsAsync(user, cancellationToken);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = dateTimeProvider.UtcNow.AddMinutes(jwt.ExpirationInMinutes),
            SigningCredentials = credentials,
            Issuer = jwt.Issuer,
            Audience = jwt.Audience
        };

        var handler = new JsonWebTokenHandler();

        string token = handler.CreateToken(tokenDescriptor);

        return token;
    }

    public string GenerateRefreshToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(randomBytes);
    }
}
