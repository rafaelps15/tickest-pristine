namespace TickestPristine.Application.Roles.GetById;

/// <summary>
/// Uma role no mesmo formato de um item de <c>GET roles</c> (spec 036, AC-13).
/// </summary>
public sealed class RoleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public bool IsDefault { get; set; }
    public bool IsAdministrator { get; set; }

    // Uma das três roles fixas (Administrador, Atendente, Colaborador), que não podem ser renomeadas nem excluídas.
    public bool IsSystem { get; set; }

    // Usuários ativos com a role, inclusive com convite pendente (spec 036, DEC-10).
    public int ActiveUserCount { get; set; }

    // Todos os usuários com a role, ativos e inativos (spec 036, DEC-4).
    public int UserCount { get; set; }

    public List<string> PermissionCodes { get; set; } = [];
}
