using System.Buffers.Text;
using System.Security.Cryptography;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Notifications;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TickestPristine.Application.Users;

/// <summary>
/// Convites de ativação da conta, num lugar só, para o cadastro, o reenvio, a ativação e a desativação: geração do
/// token, gravação só do hash, revogação dos anteriores, validade, limite de emissões e envio do e-mail.
/// </summary>
internal static class UserInvitations
{
    /// <summary>
    /// Tempo de validade do convite, contado da emissão.
    /// </summary>
    internal static readonly TimeSpan Validity = TimeSpan.FromHours(72);

    /// <summary>
    /// Convites que um usuário pode receber dentro de <see cref="LimitWindow"/>.
    /// </summary>
    internal const int MaxIssuedInWindow = 5;

    internal static readonly TimeSpan LimitWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// Bytes aleatórios do token (256 bits), gerados por CSPRNG.
    /// </summary>
    private const int TokenSizeInBytes = 32;

    /// <summary>
    /// Revoga os convites utilizáveis do usuário e cria um novo, valendo por <see cref="Validity"/>. Devolve o token em
    /// claro, que só pode ir para o e-mail. Quem chama salva o contexto.
    /// </summary>
    public static async Task<IssuedInvitation> IssueAsync(
        IApplicationDbContext context,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await RevokeAllAsync(context, userId, nowUtc, cancellationToken);

        string token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenSizeInBytes));

        var invitation = new UserInvitation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(token),
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.Add(Validity)
        };

        invitation.Raise(new UserInvitationIssuedDomainEvent(userId));

        context.UserInvitations.Add(invitation);

        return new IssuedInvitation(token, invitation.ExpiresAtUtc);
    }

    /// <summary>
    /// Marca como revogados os convites ainda não usados nem revogados do usuário. Quem chama salva o contexto.
    /// </summary>
    public static async Task RevokeAllAsync(
        IApplicationDbContext context,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        List<UserInvitation> usable = await context.UserInvitations
            .Where(i => i.UserId == userId && i.UsedAtUtc == null && i.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (UserInvitation invitation in usable)
        {
            invitation.RevokedAtUtc = nowUtc;
        }
    }

    /// <summary>
    /// Indica se o usuário já recebeu <see cref="MaxIssuedInWindow"/> convites dentro de <see cref="LimitWindow"/>.
    /// </summary>
    public static async Task<bool> HasReachedLimitAsync(
        IApplicationDbContext context,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        DateTime windowStartUtc = nowUtc.Subtract(LimitWindow);

        int issued = await context.UserInvitations
            .CountAsync(i => i.UserId == userId && i.CreatedAtUtc > windowStartUtc, cancellationToken);

        return issued >= MaxIssuedInWindow;
    }

    /// <summary>
    /// Hash do token, que é o valor gravado. Usa o mesmo SHA-256 do refresh token: o token já é aleatório e longo.
    /// </summary>
    public static string HashToken(string token) => RefreshTokenHasher.Hash(token);

    /// <summary>
    /// Envia o e-mail de convite. Uma falha no envio não desfaz o cadastro nem o reenvio: fica no log, sem o token, e o
    /// administrador pode reenviar o convite.
    /// </summary>
    public static async Task SendAsync(
        IEmailSender emailSender,
        IActivationLinkBuilder activationLinkBuilder,
        ILogger logger,
        User user,
        IssuedInvitation invitation,
        CancellationToken cancellationToken)
    {
        EmailMessage message = UserInvitationEmail.Create(
            user,
            activationLinkBuilder.Build(invitation.Token),
            invitation.ExpiresAtUtc);

        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
#pragma warning disable CA1031 // A falha no envio de qualquer tipo é registrada e não desfaz o cadastro (DEC-3 da spec 008).
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // O token não vai para o log: a mensagem cita só o usuário, e a exceção do envio traz a resposta do servidor
            // SMTP, não o corpo do e-mail.
            logger.LogError(exception, "Falha ao enviar o convite de ativação ao usuário {UserId}", user.Id);
        }
    }
}

/// <summary>
/// Convite recém-emitido: o token em claro (só para o e-mail) e a data de expiração.
/// </summary>
internal sealed record IssuedInvitation(string Token, DateTime ExpiresAtUtc);
