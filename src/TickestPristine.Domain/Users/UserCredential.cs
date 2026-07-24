using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed class UserCredential : Entity
{
    private UserCredential(Guid id, Guid userId, string passwordHash)
    {
        Id = id;
        UserId = userId;
        PasswordHash = passwordHash;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string PasswordHash { get; private set; }

    public static UserCredential Create(Guid userId, string passwordHash) =>
        new(Guid.NewGuid(), userId, passwordHash);
}
