using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Departments.GetAll;

public sealed record GetDepartmentsQuery : IQuery<List<DepartmentResponse>>;
