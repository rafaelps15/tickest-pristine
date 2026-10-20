using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.Users;

/// <summary>
/// Abertura de sessão compartilhada pelos casos de uso que autenticam o usuário.
/// </summary>
internal static class UserSessions
{
    /// <summary>
    /// Gera o access token e inclui no contexto o refresh token novo (só o hash). Quem chama salva o contexto.
    /// </summary>
    public static async Task<AccessTokensResponse> StartAsync(
        IApplicationDbContext context,
        ITokenProvider tokenProvider,
        User user,
        CancellationToken cancellationToken)
    {
        string accessToken = await tokenProvider.CreateAsync(user, cancellationToken);
        GeneratedRefreshToken generatedRefreshToken = tokenProvider.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = RefreshTokenHasher.Hash(generatedRefreshToken.Token),
            UserId = user.Id,
            ExpiresOnUtc = generatedRefreshToken.ExpiresOnUtc
        };

        refreshToken.Raise(new RefreshTokenCreatedDomainEvent(refreshToken.Id, refreshToken.UserId));

        context.RefreshTokens.Add(refreshToken);

        return new AccessTokensResponse(accessToken, generatedRefreshToken.Token);
    }

    /// <summary>
    /// Marca para remoção todos os refresh tokens do usuário (todas as sessões abertas). Quem chama salva o contexto.
    /// </summary>
    public static async Task RevokeAllAsync(IApplicationDbContext context, Guid userId, CancellationToken cancellationToken)
    {
        List<RefreshToken> refreshTokens = await context.RefreshTokens
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);

        context.RefreshTokens.RemoveRange(refreshTokens);
    }
}
