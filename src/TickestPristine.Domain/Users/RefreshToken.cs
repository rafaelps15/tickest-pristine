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
}
