using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Sectors;

public sealed class Sector : Entity
{
    private Sector(Guid id, string name, string? description, Guid departmentId)
    {
        Id = id;
        Name = name;
        Description = description;
        IsActive = true;
        DepartmentId = departmentId;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public Guid DepartmentId { get; private set; }

    public static Sector Create(string name, Guid departmentId, string? description = null) =>
        new(Guid.NewGuid(), name, description, departmentId);

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
