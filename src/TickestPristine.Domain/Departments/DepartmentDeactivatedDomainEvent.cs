using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Departments;

public sealed record DepartmentDeactivatedDomainEvent(Guid DepartmentId) : IDomainEvent;
