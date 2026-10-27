using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

/// <summary>
/// Convite de ativação da conta: o usuário cadastrado sem senha define a dele pelo link enviado por e-mail. Só o hash
/// do token fica gravado; o token em claro existe apenas no e-mail. O convite é de uso único e vale até
/// <see cref="ExpiresAtUtc"/>; emitir um novo revoga os anteriores.
/// </summary>
public sealed class UserInvitation : Entity, IAuditable
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Hash SHA-256 (hexadecimal) do token do link de ativação.
    /// </summary>
    public string TokenHash { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// Quando o convite foi usado para ativar a conta; é token de concorrência, para duas ativações simultâneas não
    /// usarem o mesmo convite.
    /// </summary>
    public DateTime? UsedAtUtc { get; set; }

    /// <summary>
    /// Quando o convite foi revogado (novo convite ou desativação do usuário); também é token de concorrência.
    /// </summary>
    public DateTime? RevokedAtUtc { get; set; }
}
