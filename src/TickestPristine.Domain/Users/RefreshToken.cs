using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed class RefreshToken : Entity
{
    public Guid Id { get; set; }
    public string Token { get; set; }
    public Guid UserId { get; set; }
    public DateTime ExpiresOnUtc { get; set; }
}
