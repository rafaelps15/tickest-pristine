using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Departments;

public sealed class Department : Entity, IAuditable
{
    private Department()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public bool IsActive { get; private set; }

    public Guid? ResponsibleUserId { get; private set; }

    public static Department Create(string name, string description, User? responsibleUser)
    {
        var department = new Department
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            IsActive = true,
            ResponsibleUserId = responsibleUser?.Id
        };

        department.Raise(new DepartmentCreatedDomainEvent(department.Id));

        return department;
    }

    /// <summary>Troca nome, descrição e responsável e dispara o evento de alteração, mesmo quando nada mudou.</summary>
    public void Update(string name, string description, User? responsibleUser)
    {
        Name = name;
        Description = description;
        ResponsibleUserId = responsibleUser?.Id;

        Raise(new DepartmentUpdatedDomainEvent(Id));
    }

    public void Deactivate()
    {
        IsActive = false;

        Raise(new DepartmentDeactivatedDomainEvent(Id));
    }
}
