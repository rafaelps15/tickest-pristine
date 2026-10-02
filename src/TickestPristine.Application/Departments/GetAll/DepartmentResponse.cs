namespace TickestPristine.Application.Departments.GetAll;

public sealed class DepartmentResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string? ResponsibleUserName { get; set; }
    public List<DepartmentSectorResponse> Sectors { get; set; } = [];
}

public sealed class DepartmentSectorResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}
