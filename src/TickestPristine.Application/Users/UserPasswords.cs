using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.Users;

/// <summary>
/// Troca de senha compartilhada pela troca comum e pela troca da senha provisória.
/// </summary>
internal static class UserPasswords
{
    /// <summary>
    /// Grava o hash da nova senha, tira a exigência de troca, gera um novo security stamp e marca para remoção todas as
    /// sessões abertas do usuário: nenhuma sessão aberta com a senha anterior continua valendo. Quem chama salva o
    /// contexto.
    /// </summary>
    public static async Task ReplaceAsync(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        UserCredential credential,
        string newPassword,
        CancellationToken cancellationToken)
    {
        credential.PasswordHash = passwordHasher.Hash(newPassword);
        credential.MustChangePassword = false;
        credential.SecurityStamp = Guid.NewGuid();

        credential.Raise(new UserPasswordChangedDomainEvent(credential.UserId));

        await UserSessions.RevokeAllAsync(context, credential.UserId, cancellationToken);
    }
}
