using TickestPristine.Domain.Departments;
using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Sectors;

public sealed class Sector : Entity, IAuditable
{
    private Sector()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public Guid DepartmentId { get; private set; }

    public static Sector Create(Department department, string name, string? description)
    {
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            DepartmentId = department.Id,
            Name = name,
            Description = description,
            IsActive = true
        };

        sector.Raise(new SectorCreatedDomainEvent(sector.Id));

        return sector;
    }

    /// <summary>Troca nome e descrição e dispara o evento de alteração, mesmo quando nada mudou.</summary>
    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;

        Raise(new SectorUpdatedDomainEvent(Id));
    }

    public void Deactivate()
    {
        IsActive = false;

        Raise(new SectorDeactivatedDomainEvent(Id));
    }
}
