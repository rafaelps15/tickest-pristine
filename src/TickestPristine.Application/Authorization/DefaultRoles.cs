namespace TickestPristine.Application.Authorization;

/// <summary>
/// Nomes e permissões iniciais das roles criadas pelo seeder numa instalação nova.
/// </summary>
public static class DefaultRoles
{
    public const string Admin = "Admin";
    public const string Agent = "Agent";
    public const string Requester = "Requester";

    public static IReadOnlyList<string> RequesterPermissions { get; } =
    [
        PermissionCodes.Tickets.Create,
        PermissionCodes.Tickets.ViewOwn,
        PermissionCodes.Tickets.UpdateOwn,
        PermissionCodes.Tickets.DeleteOwn,
        PermissionCodes.Tickets.ReopenOwn
    ];

    /// <summary>
    /// Permissões da role Agent: as do Requester mais o atendimento dos chamados de todos os usuários.
    /// </summary>
    public static IReadOnlyList<string> AgentPermissions { get; } =
        [.. RequesterPermissions, PermissionCodes.Tickets.Manage];
}
