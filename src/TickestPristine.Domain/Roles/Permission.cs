namespace TickestPristine.Domain.Roles;

/// <summary>
/// Permissão do sistema, com o nome, a descrição e o grupo exibidos na tela. As instâncias abaixo são o catálogo
/// completo e vão para a tabela de permissões pelas migrations; toda constante de <see cref="PermissionCodes"/>
/// precisa de uma (um teste confere).
/// </summary>
public sealed class Permission
{
    private const string TicketsGroup = "Chamados";
    private const string UsersGroup = "Usuários";
    private const string RolesGroup = "Funções e permissões";
    private const string OrganizationGroup = "Estrutura organizacional";
    private const string AuditGroup = "Auditoria";

    public static readonly Permission TicketsCreate = new(PermissionCodes.Tickets.Create, "Abrir chamados", "Abrir novos chamados.", TicketsGroup, isAdministrative: false);
    public static readonly Permission TicketsViewOwn = new(PermissionCodes.Tickets.ViewOwn, "Ver os próprios chamados", "Consultar os chamados que a própria pessoa abriu.", TicketsGroup, isAdministrative: false);
    public static readonly Permission TicketsUpdateOwn = new(PermissionCodes.Tickets.UpdateOwn, "Editar os próprios chamados", "Alterar os chamados que a própria pessoa abriu.", TicketsGroup, isAdministrative: false);
    public static readonly Permission TicketsDeleteOwn = new(PermissionCodes.Tickets.DeleteOwn, "Excluir os próprios chamados", "Excluir os chamados que a própria pessoa abriu.", TicketsGroup, isAdministrative: false);
    public static readonly Permission TicketsReopenOwn = new(PermissionCodes.Tickets.ReopenOwn, "Reabrir os próprios chamados", "Reabrir chamados resolvidos que a própria pessoa abriu.", TicketsGroup, isAdministrative: false);
    public static readonly Permission TicketsManage = new(PermissionCodes.Tickets.Manage, "Atender a fila de chamados", "Ver e tratar os chamados de todos os colaboradores.", TicketsGroup, isAdministrative: false);

    public static readonly Permission UsersRead = new(PermissionCodes.Users.Read, "Ver usuários", "Listar e consultar os colaboradores cadastrados.", UsersGroup);
    public static readonly Permission UsersCreate = new(PermissionCodes.Users.Create, "Cadastrar colaboradores", "Incluir novos colaboradores no sistema.", UsersGroup);
    public static readonly Permission UsersUpdate = new(PermissionCodes.Users.Update, "Editar dados de colaboradores", "Alterar nome e sobrenome de outros colaboradores.", UsersGroup);
    public static readonly Permission UsersDeactivate = new(PermissionCodes.Users.Deactivate, "Desativar e reativar colaboradores", "Bloquear ou devolver o acesso de colaboradores ao sistema.", UsersGroup);
    public static readonly Permission UsersAssignRoles = new(PermissionCodes.Users.AssignRoles, "Alterar funções de colaboradores", "Definir quais funções cada colaborador tem.", UsersGroup);

    public static readonly Permission RolesRead = new(PermissionCodes.Roles.Read, "Ver funções", "Consultar as funções existentes e as permissões de cada uma.", RolesGroup);
    public static readonly Permission RolesManage = new(PermissionCodes.Roles.Manage, "Gerenciar funções", "Criar funções e definir as permissões de cada uma.", RolesGroup);

    public static readonly Permission DepartmentsManage = new(PermissionCodes.Departments.Manage, "Gerenciar departamentos", "Criar, editar e desativar departamentos.", OrganizationGroup);
    public static readonly Permission SectorsManage = new(PermissionCodes.Sectors.Manage, "Gerenciar setores", "Criar, editar e desativar setores.", OrganizationGroup);

    public static readonly Permission AuditRead = new(PermissionCodes.Audit.Read, "Ver o log de auditoria", "Consultar quem alterou cada registro, quando e quais valores mudaram.", AuditGroup);

    public static readonly IReadOnlyList<Permission> All =
    [
        TicketsCreate, TicketsViewOwn, TicketsUpdateOwn, TicketsDeleteOwn, TicketsReopenOwn, TicketsManage,
        UsersRead, UsersCreate, UsersUpdate, UsersDeactivate, UsersAssignRoles,
        RolesRead, RolesManage,
        DepartmentsManage, SectorsManage,
        AuditRead
    ];

    private Permission(string code, string name, string description, string group, bool isAdministrative = true)
    {
        Code = code;
        Name = name;
        Description = description;
        Group = group;
        IsAdministrative = isAdministrative;
    }

    private Permission()
    {
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string Group { get; private set; }

    /// <summary>
    /// Permissão administrativa: só pode ser concedida ou removida por quem já a possui.
    /// </summary>
    public bool IsAdministrative { get; private set; }

    public static Permission? Find(string code) => All.FirstOrDefault(p => p.Code == code);

    /// <summary>
    /// Nome amigável da permissão, ou o próprio código se ele não estiver no catálogo.
    /// </summary>
    public static string GetDisplayName(string code) => Find(code)?.Name ?? code;
}
