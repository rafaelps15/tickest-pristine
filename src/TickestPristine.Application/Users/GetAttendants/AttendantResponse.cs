namespace TickestPristine.Application.Users.GetAttendants;

/// <summary>
/// Atendente na lista de escolha do responsável. Sem setor, os três campos de setor e departamento vêm nulos.
/// </summary>
public sealed record AttendantResponse
{
    public Guid Id { get; init; }
    public string FullName { get; init; }
    public string Email { get; init; }
    public Guid? SectorId { get; init; }
    public string? SectorName { get; init; }
    public string? DepartmentName { get; init; }
}
