using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.Refresh;

internal sealed class RefreshTokenCommandHandler(
    IApplicationDbContext context,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<RefreshTokenCommand, AccessTokensResponse>
{
    private static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(30);

    public async Task<Result<AccessTokensResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        string tokenHash = RefreshTokenHasher.Hash(command.RefreshToken);
        DateTime nowUtc = dateTimeProvider.UtcNow;

        RefreshToken? refreshToken = await context.RefreshTokens
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (refreshToken is null)
        {
            await RevokeSessionsIfTokenWasReusedAsync(tokenHash, nowUtc, cancellationToken);

            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        if (refreshToken.ExpiresOnUtc < nowUtc)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        // Usuário e security stamp atual numa consulta só.
        var owner = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == refreshToken.UserId)
            .Select(u => new
            {
                User = u,
                SecurityStamp = context.UserCredentials
                    .Where(c => c.UserId == u.Id)
                    .Select(c => (Guid?)c.SecurityStamp)
                    .FirstOrDefault()
            })
            .SingleAsync(cancellationToken);

        // Sessão aberta antes da última troca de senha (ex.: um login com a senha antiga que terminou depois da troca).
        if (owner.SecurityStamp != refreshToken.SecurityStamp)
        {
            context.RefreshTokens.Remove(refreshToken);

            await context.SaveChangesAsync(cancellationToken);

            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        User user = owner.User;

        if (!user.IsActive)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.Deactivated);
        }

        string accessToken = await tokenProvider.CreateAsync(user, cancellationToken);
        GeneratedRefreshToken generatedRefreshToken = tokenProvider.GenerateRefreshToken();

        // Troca o refresh token para que o anterior não possa ser reutilizado; a validade nunca passa do limite da sessão.
        refreshToken.PreviousTokenHash = refreshToken.TokenHash;
        refreshToken.RotatedAtUtc = nowUtc;
        refreshToken.TokenHash = RefreshTokenHasher.Hash(generatedRefreshToken.Token);
        refreshToken.ExpiresOnUtc = UserSessions.Earliest(generatedRefreshToken.ExpiresOnUtc, refreshToken.SessionExpiresOnUtc);

        refreshToken.Raise(new RefreshTokenRotatedDomainEvent(refreshToken.Id, refreshToken.UserId));

        await context.SaveChangesAsync(cancellationToken);

        return new AccessTokensResponse(accessToken, generatedRefreshToken.Token);
    }

    /// <summary>
    /// Token já trocado numa renovação e usado de novo: alguém ficou com uma cópia, então todas as sessões do usuário são
    /// revogadas. Dentro de <see cref="ReuseGracePeriod"/> depois da troca, é tratado como duas abas renovando juntas e
    /// só é recusado.
    /// </summary>
    private async Task RevokeSessionsIfTokenWasReusedAsync(string tokenHash, DateTime nowUtc, CancellationToken cancellationToken)
    {
        RefreshToken? rotatedToken = await context.RefreshTokens
            .SingleOrDefaultAsync(rt => rt.PreviousTokenHash == tokenHash, cancellationToken);

        if (rotatedToken is null || rotatedToken.RotatedAtUtc > nowUtc.Subtract(ReuseGracePeriod))
        {
            return;
        }

        await UserSessions.RevokeAllAsync(context, rotatedToken.UserId, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }
}
