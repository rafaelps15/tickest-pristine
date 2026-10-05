namespace TickestPristine.Application.Authorization;

/// <summary>
/// Catálogo de todas as permissões, com o nome, a descrição e o grupo exibidos na tela. Toda constante de
/// <see cref="PermissionCodes"/> precisa estar aqui (o teste do catálogo confere).
/// </summary>
public static class PermissionCatalog
{
    private const string TicketsGroup = "Chamados";
    private const string UsersGroup = "Usuários";
    private const string RolesGroup = "Funções e permissões";
    private const string OrganizationGroup = "Estrutura organizacional";
    private const string AuditGroup = "Auditoria";

    public static IReadOnlyList<PermissionDefinition> Definitions { get; } =
    [
        new(PermissionCodes.Tickets.Create, "Abrir chamados", "Abrir novos chamados.", TicketsGroup, IsAdministrative: false),
        new(PermissionCodes.Tickets.ViewOwn, "Ver os próprios chamados", "Consultar os chamados que a própria pessoa abriu.", TicketsGroup, IsAdministrative: false),
        new(PermissionCodes.Tickets.UpdateOwn, "Editar os próprios chamados", "Alterar os chamados que a própria pessoa abriu.", TicketsGroup, IsAdministrative: false),
        new(PermissionCodes.Tickets.DeleteOwn, "Excluir os próprios chamados", "Excluir os chamados que a própria pessoa abriu.", TicketsGroup, IsAdministrative: false),
        new(PermissionCodes.Tickets.ReopenOwn, "Reabrir os próprios chamados", "Reabrir chamados resolvidos que a própria pessoa abriu.", TicketsGroup, IsAdministrative: false),
        new(PermissionCodes.Tickets.Manage, "Atender a fila de chamados", "Ver e tratar os chamados de todos os colaboradores.", TicketsGroup, IsAdministrative: false),

        new(PermissionCodes.Users.Read, "Ver usuários", "Listar e consultar os colaboradores cadastrados.", UsersGroup),
        new(PermissionCodes.Users.Create, "Cadastrar colaboradores", "Incluir novos colaboradores no sistema.", UsersGroup),
        new(PermissionCodes.Users.Update, "Editar dados de colaboradores", "Alterar nome e sobrenome de outros colaboradores.", UsersGroup),
        new(PermissionCodes.Users.Deactivate, "Desativar e reativar colaboradores", "Bloquear ou devolver o acesso de colaboradores ao sistema.", UsersGroup),
        new(PermissionCodes.Users.AssignRoles, "Alterar funções de colaboradores", "Definir quais funções cada colaborador tem.", UsersGroup),

        new(PermissionCodes.Roles.Read, "Ver funções", "Consultar as funções existentes e as permissões de cada uma.", RolesGroup),
        new(PermissionCodes.Roles.Manage, "Gerenciar funções", "Criar funções e definir as permissões de cada uma.", RolesGroup),

        new(PermissionCodes.Departments.Manage, "Gerenciar departamentos", "Criar, editar e desativar departamentos.", OrganizationGroup),
        new(PermissionCodes.Sectors.Manage, "Gerenciar setores", "Criar, editar e desativar setores.", OrganizationGroup),

        new(PermissionCodes.Audit.Read, "Ver o log de auditoria", "Consultar quem alterou cada registro, quando e quais valores mudaram.", AuditGroup)
    ];

    private static readonly Dictionary<string, PermissionDefinition> DefinitionsByCode =
        Definitions.ToDictionary(d => d.Code, StringComparer.Ordinal);

    public static IReadOnlyList<string> All { get; } = Definitions.Select(d => d.Code).ToList();

    /// <summary>
    /// Permissões administrativas: só podem ser concedidas ou removidas por quem já as possui.
    /// </summary>
    public static IReadOnlyList<string> Administrative { get; } =
        Definitions.Where(d => d.IsAdministrative).Select(d => d.Code).ToList();

    public static bool Contains(string code) => DefinitionsByCode.ContainsKey(code);

    public static bool IsAdministrative(string code) =>
        DefinitionsByCode.TryGetValue(code, out PermissionDefinition? definition) && definition.IsAdministrative;

    /// <summary>
    /// Retorna o nome amigável da permissão, ou o próprio código se ele não estiver no catálogo.
    /// </summary>
    public static string GetDisplayName(string code) =>
        DefinitionsByCode.TryGetValue(code, out PermissionDefinition? definition) ? definition.Name : code;
}
