using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

public sealed class Role : Entity
{
    /// <summary>
    /// Role de administrador: recebe todas as permissões do catálogo.
    /// </summary>
    public static Role Administrator => new()
    {
        Id = new Guid("0199a7c4-0000-7000-8000-000000000001"),
        Name = "Administrador",
        IsAdministrator = true
    };

    /// <summary>
    /// Role de quem atende a fila de chamados.
    /// </summary>
    public static Role Agent => new()
    {
        Id = new Guid("0199a7c4-0000-7000-8000-000000000002"),
        Name = "Atendente"
    };

    /// <summary>
    /// Role padrão de quem se cadastra.
    /// </summary>
    public static Role Collaborator => new()
    {
        Id = new Guid("0199a7c4-0000-7000-8000-000000000003"),
        Name = "Colaborador",
        IsDefault = true
    };

    public Guid Id { get; set; }
    public string Name { get; set; }
    public bool IsDefault { get; set; }
    public bool IsAdministrator { get; set; }
    public int Version { get; set; }
}
