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
    /// Duração máxima de uma sessão, contada do login: depois dela, só um novo login devolve tokens.
    /// </summary>
    internal static readonly TimeSpan MaxSessionDuration = TimeSpan.FromDays(30);

    /// <summary>
    /// Gera o access token e inclui no contexto o refresh token novo (só o hash), ligado ao
    /// <paramref name="securityStamp"/> da credencial. Também marca para remoção os refresh tokens já expirados do
    /// usuário, para a tabela não crescer a cada login. Quem chama salva o contexto.
    /// </summary>
    public static async Task<AccessTokensResponse> StartAsync(
        IApplicationDbContext context,
        ITokenProvider tokenProvider,
        User user,
        Guid securityStamp,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        List<RefreshToken> expiredRefreshTokens = await context.RefreshTokens
            .Where(r => r.UserId == user.Id && r.ExpiresOnUtc < nowUtc)
            .ToListAsync(cancellationToken);

        context.RefreshTokens.RemoveRange(expiredRefreshTokens);

        string accessToken = await tokenProvider.CreateAsync(user, cancellationToken);
        GeneratedRefreshToken generatedRefreshToken = tokenProvider.GenerateRefreshToken();
        DateTime sessionExpiresOnUtc = nowUtc.Add(MaxSessionDuration);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = RefreshTokenHasher.Hash(generatedRefreshToken.Token),
            UserId = user.Id,
            ExpiresOnUtc = Earliest(generatedRefreshToken.ExpiresOnUtc, sessionExpiresOnUtc),
            SessionExpiresOnUtc = sessionExpiresOnUtc,
            SecurityStamp = securityStamp
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

        foreach (RefreshToken refreshToken in refreshTokens)
        {
            refreshToken.Raise(new RefreshTokenRevokedDomainEvent(refreshToken.Id, refreshToken.UserId));
        }

        context.RefreshTokens.RemoveRange(refreshTokens);
    }

    /// <summary>
    /// A data mais próxima entre as duas: a validade de um refresh token nunca passa do limite da sessão.
    /// </summary>
    public static DateTime Earliest(DateTime first, DateTime second) => first < second ? first : second;
}
