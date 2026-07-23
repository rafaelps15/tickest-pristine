namespace TickestPristine.Domain.Departments;

public sealed class Department
{
    private Department(Guid id, string name, string description, Guid? responsibleUserId)
    {
        Id = id;
        Name = name;
        Description = description;
        IsActive = true;
        ResponsibleUserId = responsibleUserId;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? ResponsibleUserId { get; private set; }

    public static Department Create(string name, string description, Guid? responsibleUserId = null) =>
        new(Guid.NewGuid(), name, description, responsibleUserId);

    public void Update(string name, string description, Guid? responsibleUserId)
    {
        Name = name;
        Description = description;
        ResponsibleUserId = responsibleUserId;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
