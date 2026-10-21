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
    IDateTimeProvider dateTimeProvider) : ITokenProvider
{
    public Task<string> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        JwtOptions jwt = jwtOptions.Value;

        var credentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256);

        // Só a identidade vai no token: as permissões são resolvidas no servidor a cada requisição e os clientes as
        // leem em GET /users/me.
        Claim[] claims =
        [
            new(TokenClaimTypes.UserId, user.Id.ToString()),
            new(TokenClaimTypes.Email, user.Email),
            new(TokenClaimTypes.Name, $"{user.FirstName} {user.LastName}")
        ];

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = dateTimeProvider.UtcNow.AddMinutes(jwt.ExpirationInMinutes),
            SigningCredentials = credentials,
            Issuer = jwt.Issuer,
            Audience = jwt.Audience
        };

        var handler = new JsonWebTokenHandler();

        return Task.FromResult(handler.CreateToken(tokenDescriptor));
    }

    public GeneratedRefreshToken GenerateRefreshToken()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);

        return new GeneratedRefreshToken(
            Convert.ToBase64String(randomBytes),
            dateTimeProvider.UtcNow.AddDays(jwtOptions.Value.RefreshTokenExpirationInDays));
    }
}
