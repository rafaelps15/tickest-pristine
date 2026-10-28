namespace TickestPristine.Application.Permissions;

/// <summary>
/// Catálogo completo das permissões, com o nome, a descrição e o grupo exibidos na tela. É a fonte da tabela de
/// permissões (pelas migrations) e das conferências que precisam saber se uma permissão é administrativa; toda
/// constante de <see cref="PermissionCodes"/> precisa de uma entrada (um teste confere).
/// </summary>
public static class PermissionCatalog
{
    private const string TicketsGroup = "Chamados";
    private const string UsersGroup = "Usuários";
    private const string RolesGroup = "Funções e permissões";
    private const string OrganizationGroup = "Estrutura organizacional";
    private const string AuditGroup = "Auditoria";

    public static readonly PermissionCatalogEntry TicketsCreate = new(PermissionCodes.Tickets.Create, "Abrir chamados", "Abrir novos chamados.", TicketsGroup, IsAdministrative: false);
    public static readonly PermissionCatalogEntry TicketsViewOwn = new(PermissionCodes.Tickets.ViewOwn, "Ver os próprios chamados", "Consultar os chamados que a própria pessoa abriu.", TicketsGroup, IsAdministrative: false);
    public static readonly PermissionCatalogEntry TicketsUpdateOwn = new(PermissionCodes.Tickets.UpdateOwn, "Editar os próprios chamados", "Alterar os chamados que a própria pessoa abriu.", TicketsGroup, IsAdministrative: false);
    public static readonly PermissionCatalogEntry TicketsReopenOwn = new(PermissionCodes.Tickets.ReopenOwn, "Reabrir os próprios chamados", "Reabrir chamados resolvidos que a própria pessoa abriu.", TicketsGroup, IsAdministrative: false);
    public static readonly PermissionCatalogEntry TicketsManage = new(PermissionCodes.Tickets.Manage, "Atender a fila de chamados", "Ver e tratar os chamados de todos os colaboradores.", TicketsGroup, IsAdministrative: false);
    public static readonly PermissionCatalogEntry TicketsAssign = new(PermissionCodes.Tickets.Assign, "Atribuir chamados", "Definir outra pessoa como responsável por um chamado.", TicketsGroup);
    public static readonly PermissionCatalogEntry TicketsCancel = new(PermissionCodes.Tickets.Cancel, "Cancelar chamados", "Cancelar qualquer chamado ativo, a qualquer momento, inclusive depois da janela em que o solicitante pode cancelar o próprio.", TicketsGroup);
    public static readonly PermissionCatalogEntry TicketsDelete = new(PermissionCodes.Tickets.Delete, "Excluir chamados", "Excluir chamados ativos de qualquer colaborador.", TicketsGroup);

    public static readonly PermissionCatalogEntry UsersRead = new(PermissionCodes.Users.Read, "Ver usuários", "Listar e consultar os colaboradores cadastrados.", UsersGroup);
    public static readonly PermissionCatalogEntry UsersCreate = new(PermissionCodes.Users.Create, "Cadastrar colaboradores", "Incluir novos colaboradores no sistema.", UsersGroup);
    public static readonly PermissionCatalogEntry UsersUpdate = new(PermissionCodes.Users.Update, "Editar dados de colaboradores", "Alterar nome e sobrenome de outros colaboradores.", UsersGroup);
    public static readonly PermissionCatalogEntry UsersDeactivate = new(PermissionCodes.Users.Deactivate, "Desativar e reativar colaboradores", "Bloquear ou devolver o acesso de colaboradores ao sistema.", UsersGroup);
    public static readonly PermissionCatalogEntry UsersAssignRoles = new(PermissionCodes.Users.AssignRoles, "Alterar funções de colaboradores", "Definir quais funções cada colaborador tem.", UsersGroup);

    public static readonly PermissionCatalogEntry RolesRead = new(PermissionCodes.Roles.Read, "Ver funções", "Consultar as funções existentes e as permissões de cada uma.", RolesGroup);
    public static readonly PermissionCatalogEntry RolesManage = new(PermissionCodes.Roles.Manage, "Gerenciar funções", "Definir as permissões de cada função.", RolesGroup);

    public static readonly PermissionCatalogEntry DepartmentsManage = new(PermissionCodes.Departments.Manage, "Gerenciar departamentos", "Criar, editar e desativar departamentos.", OrganizationGroup);
    public static readonly PermissionCatalogEntry SectorsManage = new(PermissionCodes.Sectors.Manage, "Gerenciar setores", "Criar, editar e desativar setores.", OrganizationGroup);

    public static readonly PermissionCatalogEntry AuditRead = new(PermissionCodes.Audit.Read, "Ver o log de auditoria", "Consultar quem alterou cada registro, quando e quais valores mudaram.", AuditGroup);

    public static readonly IReadOnlyList<PermissionCatalogEntry> All =
    [
        TicketsCreate, TicketsViewOwn, TicketsUpdateOwn, TicketsReopenOwn, TicketsManage, TicketsAssign, TicketsCancel, TicketsDelete,
        UsersRead, UsersCreate, UsersUpdate, UsersDeactivate, UsersAssignRoles,
        RolesRead, RolesManage,
        DepartmentsManage, SectorsManage,
        AuditRead
    ];

    public static PermissionCatalogEntry? Find(string code) => All.FirstOrDefault(p => p.Code == code);

    /// <summary>
    /// Nome amigável da permissão, ou o próprio código se ele não estiver no catálogo.
    /// </summary>
    public static string GetDisplayName(string code) => Find(code)?.Name ?? code;
}

/// <summary>
/// Uma permissão do catálogo. Administrativa: só pode ser concedida ou removida por quem já a possui.
/// </summary>
public sealed record PermissionCatalogEntry(
    string Code,
    string Name,
    string Description,
    string Group,
    bool IsAdministrative = true);
