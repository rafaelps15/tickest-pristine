using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed class Role : Entity, IAuditable
{
    public static readonly Role Administrator = new(new Guid("0199a7c4-0000-7000-8000-000000000001"), "Administrador");
    public static readonly Role Agent = new(new Guid("0199a7c4-0000-7000-8000-000000000002"), "Atendente");
    public static readonly Role Collaborator = new(new Guid("0199a7c4-0000-7000-8000-000000000003"), "Colaborador");

    public Role(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; init; }
    public string Name { get; init; }
    public int Version { get; set; }
}
