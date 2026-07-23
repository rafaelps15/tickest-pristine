using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Departments.GetById;

public sealed record GetDepartmentByIdQuery(Guid DepartmentId) : IQuery<DepartmentResponse>;
