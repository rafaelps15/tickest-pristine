using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Users;

public sealed class UserRole : Entity
{
    private UserRole(Guid id, Guid userId, Guid roleId)
    {
        Id = id;
        UserId = userId;
        RoleId = roleId;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }

    public static UserRole Create(Guid userId, Guid roleId) =>
        new(Guid.NewGuid(), userId, roleId);
}
