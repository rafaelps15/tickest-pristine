namespace TickestPristine.Application.Authorization;

/// <summary>
/// Dados de uma permissão: código, nome, descrição, grupo e se é administrativa (padrão: sim).
/// </summary>
public sealed record PermissionDefinition(
    string Code,
    string Name,
    string Description,
    string Group,
    bool IsAdministrative = true);
