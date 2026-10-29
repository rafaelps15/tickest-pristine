using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed class Role : Entity, IAuditable
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}
