using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.ChangeRequiredPassword;

/// <summary>
/// Troca a senha provisória de quem o login barrou com <see cref="UserErrors.PasswordChangeRequired"/>. Como o usuário
/// ainda não tem token, as credenciais são conferidas de novo; com a troca feita, a sessão já é aberta.
/// </summary>
internal sealed class ChangeRequiredUserPasswordCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ChangeRequiredUserPasswordCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> Handle(ChangeRequiredUserPasswordCommand command, CancellationToken cancellationToken)
    {
        Result<VerifiedCredentials> verification = await UserCredentialsVerifier.VerifyAsync(
            context,
            passwordHasher,
            command.Email,
            command.CurrentPassword,
            dateTimeProvider.UtcNow,
            cancellationToken);

        if (verification.IsFailure)
        {
            return Result.Failure<AccessTokensResponse>(verification.Error);
        }

        (User user, UserCredential credential) = verification.Value;

        if (!user.IsActive)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.Deactivated);
        }

        if (!credential.MustChangePassword)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.PasswordChangeNotRequired);
        }

        credential.PasswordHash = passwordHasher.Hash(command.NewPassword);
        credential.MustChangePassword = false;
        credential.SecurityStamp = Guid.NewGuid();

        credential.Raise(new UserPasswordChangedDomainEvent(credential.UserId));

        // Como na troca de senha comum, nenhuma sessão aberta com a senha anterior continua valendo.
        await UserSessions.RevokeAllAsync(context, user.Id, cancellationToken);

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
