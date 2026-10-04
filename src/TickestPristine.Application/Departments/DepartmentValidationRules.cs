namespace TickestPristine.Application.Departments;

/// <summary>
/// Limites dos campos do departamento, iguais na criação e na edição.
/// </summary>
internal static class DepartmentValidationRules
{
    public const int NameMaxLength = 100;

    public const int DescriptionMaxLength = 500;
}
