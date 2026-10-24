using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed class RefreshToken : Entity
{
    public Guid Id { get; set; }
    /// <summary>
    /// Hash SHA-256 do refresh token. O valor original só existe na resposta enviada ao cliente.
    /// </summary>
    public string TokenHash { get; set; }
    public Guid UserId { get; set; }
    public DateTime ExpiresOnUtc { get; set; }

    /// <summary>
    /// Limite absoluto da sessão, contado do login: as renovações nunca levam o token além dele.
    /// </summary>
    public DateTime SessionExpiresOnUtc { get; set; }

    /// <summary>
    /// Hash do token trocado na última renovação. Se ele voltar a ser usado, alguém ficou com uma cópia.
    /// </summary>
    public string? PreviousTokenHash { get; set; }

    public DateTime? RotatedAtUtc { get; set; }

    /// <summary>
    /// <see cref="UserCredential.SecurityStamp"/> de quando a sessão foi aberta.
    /// </summary>
    public Guid SecurityStamp { get; set; }
}
