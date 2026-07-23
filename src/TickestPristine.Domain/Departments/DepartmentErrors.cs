using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Departments;

public static class DepartmentErrors
{
    public static Error NotFound(Guid departmentId) => Error.NotFound(
        "Departments.NotFound",
        $"The department with the Id = '{departmentId}' was not found");
}
