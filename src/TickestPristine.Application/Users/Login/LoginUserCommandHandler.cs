using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Users;
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
        Result<VerifiedCredentials> verification = await UserCredentialsVerifier.VerifyAsync(
            context,
            passwordHasher,
            command.Email,
            command.Password,
            dateTimeProvider.UtcNow,
            cancellationToken);

        if (verification.IsFailure)
        {
            return Result.Failure<AccessTokensResponse>(verification.Error);
        }

        (User user, UserCredential credential) = verification.Value;

        // Checado só depois da senha, para não revelar a situação da conta a quem não a conhece.
        if (!user.IsActive)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.Deactivated);
        }

        // Senha provisória: o cliente troca a senha em POST users/login/new-password, que já devolve os tokens.
        if (credential.MustChangePassword)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.PasswordChangeRequired);
        }

        AccessTokensResponse tokens = await UserSessions.StartAsync(
            context,
            tokenProvider,
            user,
            credential.SecurityStamp,
            dateTimeProvider.UtcNow,
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return tokens;
    }
}
