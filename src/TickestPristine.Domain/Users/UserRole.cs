using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed class UserRole : Entity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}
