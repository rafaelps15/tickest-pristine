using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users;

/// <summary>
/// Conferência de e-mail e senha compartilhada pelo login e pela troca da senha provisória, com o bloqueio temporário
/// da conta depois de várias senhas erradas seguidas.
/// </summary>
internal static class UserCredentialsVerifier
{
    internal const int MaxFailedAttempts = 5;

    internal static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Devolve o usuário e a credencial (rastreada) quando a senha confere. Senha errada conta uma tentativa e é gravada
    /// na hora, porque quem chama não salva em caso de falha; a <see cref="MaxFailedAttempts"/>ª seguida bloqueia a conta
    /// por <see cref="LockoutDuration"/>. Com a conta bloqueada, a senha nem é conferida.
    /// </summary>
    public static async Task<Result<VerifiedCredentials>> VerifyAsync(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        string email,
        string password,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        User? user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            // Gasta o mesmo tempo de uma conferência de senha, para o tempo de resposta não revelar quais e-mails existem.
            _ = passwordHasher.Hash(password);

            return Result.Failure<VerifiedCredentials>(UserErrors.InvalidCredentials);
        }

        UserCredential? credential = await context.UserCredentials
            .SingleOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);

        if (credential is null)
        {
            return Result.Failure<VerifiedCredentials>(UserErrors.InvalidCredentials);
        }

        if (credential.LockoutEndsAtUtc > nowUtc)
        {
            return Result.Failure<VerifiedCredentials>(UserErrors.LockedOut);
        }

        if (!passwordHasher.Verify(password, credential.PasswordHash))
        {
            credential.FailedLoginAttempts++;

            if (credential.FailedLoginAttempts >= MaxFailedAttempts)
            {
                credential.FailedLoginAttempts = 0;
                credential.LockoutEndsAtUtc = nowUtc.Add(LockoutDuration);
            }

            await context.SaveChangesAsync(cancellationToken);

            return Result.Failure<VerifiedCredentials>(
                credential.LockoutEndsAtUtc > nowUtc ? UserErrors.LockedOut : UserErrors.InvalidCredentials);
        }

        credential.FailedLoginAttempts = 0;
        credential.LockoutEndsAtUtc = null;

        return new VerifiedCredentials(user, credential);
    }
}

internal sealed record VerifiedCredentials(User User, UserCredential Credential);
