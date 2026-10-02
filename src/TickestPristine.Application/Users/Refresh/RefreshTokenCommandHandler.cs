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
    public async Task<Result<AccessTokensResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        string tokenHash = RefreshTokenHasher.Hash(command.RefreshToken);

        RefreshToken? refreshToken = await context.RefreshTokens
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (refreshToken is null || refreshToken.ExpiresOnUtc < dateTimeProvider.UtcNow)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidRefreshToken);
        }

        User user = await context.Users
            .AsNoTracking()
            .SingleAsync(u => u.Id == refreshToken.UserId, cancellationToken);

        if (!user.IsActive)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.Deactivated);
        }

        string accessToken = await tokenProvider.CreateAsync(user, cancellationToken);
        GeneratedRefreshToken generatedRefreshToken = tokenProvider.GenerateRefreshToken();

        // Troca o refresh token para que o anterior não possa ser reutilizado.
        refreshToken.TokenHash = RefreshTokenHasher.Hash(generatedRefreshToken.Token);
        refreshToken.ExpiresOnUtc = generatedRefreshToken.ExpiresOnUtc;

        refreshToken.Raise(new RefreshTokenRotatedDomainEvent(refreshToken.Id, refreshToken.UserId));

        await context.SaveChangesAsync(cancellationToken);

        return new AccessTokensResponse(accessToken, generatedRefreshToken.Token);
    }
}
