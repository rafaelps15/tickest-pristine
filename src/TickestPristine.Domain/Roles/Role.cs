using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed class Role : Entity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public bool IsDefault { get; set; }
    public bool IsAdministrator { get; set; }
    public int Version { get; set; }
}
