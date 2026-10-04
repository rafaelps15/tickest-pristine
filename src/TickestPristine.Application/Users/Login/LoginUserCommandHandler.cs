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
    ITokenProvider tokenProvider) : ICommandHandler<LoginUserCommand, AccessTokensResponse>
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

        // Senha provisória: o cliente troca a senha em POST users/login/new-password, que já devolve os tokens.
        if (credential.MustChangePassword)
        {
            return Result.Failure<AccessTokensResponse>(UserErrors.PasswordChangeRequired);
        }

        AccessTokensResponse tokens = await UserSessions.StartAsync(context, tokenProvider, user, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return tokens;
    }
}
