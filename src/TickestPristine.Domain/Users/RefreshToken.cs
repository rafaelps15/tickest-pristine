using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed class RefreshToken : Entity
{
    private RefreshToken(Guid id, string token, Guid userId, DateTime expiresOnUtc)
    {
        Id = id;
        Token = token;
        UserId = userId;
        ExpiresOnUtc = expiresOnUtc;
        User = null!;
    }

    public Guid Id { get; private set; }
    public string Token { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime ExpiresOnUtc { get; private set; }
    public User User { get; private set; }

    public static RefreshToken Create(string token, Guid userId, DateTime expiresOnUtc) =>
        new(Guid.NewGuid(), token, userId, expiresOnUtc);

    public void Rotate(string token, DateTime expiresOnUtc)
    {
        Token = token;
        ExpiresOnUtc = expiresOnUtc;
    }
}
