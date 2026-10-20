using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.ChangeRequiredPassword;

/// <summary>
/// Troca a senha provisória de quem o login barrou com <see cref="UserErrors.PasswordChangeRequired"/>. Como o usuário
/// ainda não tem token, as credenciais são conferidas de novo; com a troca feita, a sessão já é aberta.
/// </summary>
internal sealed class ChangeRequiredUserPasswordCommandHandler(
    IApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider) : ICommandHandler<ChangeRequiredUserPasswordCommand, AccessTokensResponse>
{
    public async Task<Result<AccessTokensResponse>> Handle(ChangeRequiredUserPasswordCommand command, CancellationToken cancellationToken)
    {
        User? user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == command.Email, cancellationToken);

        if (user is null)
        {
            // Gasta o mesmo tempo de uma conferência de senha, para o tempo de resposta não revelar quais e-mails existem.
            _ = passwordHasher.Hash(command.CurrentPassword);

            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidCredentials);
        }

        UserCredential? credential = await context.UserCredentials
            .SingleOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);

        if (credential is null || !passwordHasher.Verify(command.CurrentPassword, credential.PasswordHash))
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.InvalidCredentials);
        }

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

        credential.Raise(new UserPasswordChangedDomainEvent(credential.UserId));

        // Como na troca de senha comum, nenhuma sessão aberta com a senha anterior continua valendo.
        await UserSessions.RevokeAllAsync(context, user.Id, cancellationToken);

        AccessTokensResponse tokens = await UserSessions.StartAsync(context, tokenProvider, user, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return tokens;
    }
}
