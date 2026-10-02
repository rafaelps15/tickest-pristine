namespace TickestPristine.Application.Authorization;

public static class PermissionCodes
{
    public static class Tickets
    {
        public const string Create = "tickets:create";
        public const string ViewOwn = "tickets:view-own";
        public const string UpdateOwn = "tickets:update-own";
        public const string Manage = "tickets:manage";
        public const string DeleteOwn = "tickets:delete-own";
        public const string ReopenOwn = "tickets:reopen-own";
    }

    public static class Users
    {
        public const string Read = "users:read";
        public const string Create = "users:create";
        public const string Update = "users:update";
        public const string Deactivate = "users:deactivate";
        public const string AssignRoles = "users:assign-roles";
    }

    public static class Roles
    {
        public const string Read = "roles:read";
        public const string Manage = "roles:manage";
    }

    public static class Departments
    {
        public const string Manage = "departments:manage";
    }

    public static class Sectors
    {
        public const string Manage = "sectors:manage";
    }

    private const string TicketsGroup = "Chamados";
    private const string UsersGroup = "Usuários";
    private const string RolesGroup = "Funções e permissões";
    private const string OrganizationGroup = "Estrutura organizacional";

    /// <summary>
    /// Catálogo de todas as permissões, com o nome, a descrição e o grupo exibidos na tela.
    /// </summary>
    public static IReadOnlyList<PermissionDefinition> Definitions { get; } =
    [
        new(Tickets.Create, "Abrir chamados", "Abrir novos chamados.", TicketsGroup, IsAdministrative: false),
        new(Tickets.ViewOwn, "Ver os próprios chamados", "Consultar os chamados que a própria pessoa abriu.", TicketsGroup, IsAdministrative: false),
        new(Tickets.UpdateOwn, "Editar os próprios chamados", "Alterar os chamados que a própria pessoa abriu.", TicketsGroup, IsAdministrative: false),
        new(Tickets.DeleteOwn, "Excluir os próprios chamados", "Excluir os chamados que a própria pessoa abriu.", TicketsGroup, IsAdministrative: false),
        new(Tickets.ReopenOwn, "Reabrir os próprios chamados", "Reabrir chamados resolvidos que a própria pessoa abriu.", TicketsGroup, IsAdministrative: false),
        new(Tickets.Manage, "Atender a fila de chamados", "Ver e tratar os chamados de todos os colaboradores.", TicketsGroup, IsAdministrative: false),

        new(Users.Read, "Ver usuários", "Listar e consultar os colaboradores cadastrados.", UsersGroup),
        new(Users.Create, "Cadastrar colaboradores", "Incluir novos colaboradores no sistema.", UsersGroup),
        new(Users.Update, "Editar dados de colaboradores", "Alterar nome e sobrenome de outros colaboradores.", UsersGroup),
        new(Users.Deactivate, "Desativar e reativar colaboradores", "Bloquear ou devolver o acesso de colaboradores ao sistema.", UsersGroup),
        new(Users.AssignRoles, "Alterar funções de colaboradores", "Definir quais funções cada colaborador tem.", UsersGroup),

        new(Roles.Read, "Ver funções", "Consultar as funções existentes e as permissões de cada uma.", RolesGroup),
        new(Roles.Manage, "Gerenciar funções", "Criar funções e definir as permissões de cada uma.", RolesGroup),

        new(Departments.Manage, "Gerenciar departamentos", "Criar, editar e desativar departamentos.", OrganizationGroup),
        new(Sectors.Manage, "Gerenciar setores", "Criar, editar e desativar setores.", OrganizationGroup)
    ];

    public static IReadOnlyList<string> All { get; } = Definitions.Select(d => d.Code).ToList();

    /// <summary>
    /// Retorna o nome amigável da permissão, ou o próprio código se ele não estiver no catálogo.
    /// </summary>
    public static string GetDisplayName(string code) =>
        Definitions.FirstOrDefault(d => d.Code == code)?.Name ?? code;

    /// <summary>
    /// Permissões administrativas: só podem ser concedidas ou removidas por quem já as possui.
    /// </summary>
    public static IReadOnlyList<string> Administrative { get; } =
        Definitions.Where(d => d.IsAdministrative).Select(d => d.Code).ToList();
}
