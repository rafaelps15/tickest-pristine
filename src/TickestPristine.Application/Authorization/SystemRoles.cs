namespace TickestPristine.Application.Authorization;

/// <summary>
/// Roles de que o código depende. Ficam nas migrations (HasData) com id fixo e não podem ser editadas pela API.
/// As demais roles (inclusive as iniciais, criadas pelo seed) são dados geridos pelos administradores.
/// </summary>
public static class SystemRoles
{
    /// <summary>
    /// Role de administrador: recebe sempre todas as permissões do catálogo. O seed do primeiro administrador e os
    /// guards dependem deste id.
    /// </summary>
    public static readonly Guid AdministratorId = new("0199a7c4-0000-7000-8000-000000000001");

    public const string AdministratorName = "Administrador";
}
