namespace TickestPristine.Domain.Roles;

public sealed class Role
{
    public static readonly Role Administrator = new("Administrador");
    public static readonly Role Agent = new("Atendente");
    public static readonly Role Collaborator = new("Colaborador");

    private Role(string name)
    {
        Name = name;
    }

    private Role()
    {
    }

    public string Name { get; private set; }
}
