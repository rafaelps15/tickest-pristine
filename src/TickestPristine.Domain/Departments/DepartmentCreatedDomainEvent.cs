using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Departments;

public sealed record DepartmentCreatedDomainEvent(Guid DepartmentId) : IDomainEvent;
