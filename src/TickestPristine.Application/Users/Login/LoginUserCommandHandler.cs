using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.Login;

internal sealed class LoginUserCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<LoginUserCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == command.Email, cancellationToken);

        if (user is null)
        {
            // Gasta o mesmo tempo de uma conferência de senha, para o tempo de resposta não revelar quais e-mails existem.
            _ = passwordHasher.Hash(command.Password);

            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidCredentials);
        }

        UserCredential? credential = await context.UserCredentials
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);

        if (credential is null || !passwordHasher.Verify(command.Password, credential.PasswordHash))
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidCredentials);
        }

        // Checado só depois da senha, para não revelar a situação da conta a quem não a conhece.
        if (!user.IsActive)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.Deactivated);
        }

        string accessToken = await tokenProvider.CreateAsync(user, cancellationToken);
        string refreshToken = tokenProvider.GenerateRefreshToken();

        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = refreshToken,
            UserId = user.Id,
            ExpiresOnUtc = dateTimeProvider.UtcNow.AddDays(RefreshTokenExpirationInDays)
        };

        newRefreshToken.Raise(new RefreshTokenCreatedDomainEvent(newRefreshToken.Id, newRefreshToken.UserId));

        context.RefreshTokens.Add(newRefreshToken);

        await context.SaveChangesAsync(cancellationToken);

        return new AccessTokensResponse(accessToken, refreshToken);
    }

    private const int RefreshTokenExpirationInDays = 7;
}
