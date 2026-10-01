using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed class UserCredential : Entity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PasswordHash { get; set; }
}
