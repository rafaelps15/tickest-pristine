using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Departments;

public static class DepartmentErrors
{
    public static Error NotFound(Guid departmentId) => Error.NotFound(
        "Departments.NotFound",
        $"O departamento com o Id = '{departmentId}' não foi encontrado");
}
