using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Departments;

public static class DepartmentErrors
{
    public static Error NotFound(Guid departmentId) => Error.NotFound(
        "Departments.NotFound",
        $"O departamento com o Id = '{departmentId}' não foi encontrado");

    public static Error NameNotUnique(string name) => Error.Conflict(
        "Departments.NameNotUnique",
        $"Já existe um departamento ativo com o nome '{name}'.");

    public static Error HasActiveTickets() => Error.Conflict(
        "Departments.HasActiveTickets",
        "O departamento tem chamados em aberto ou em andamento nos seus setores. Conclua ou cancele esses chamados antes de desativá-lo.");
}
