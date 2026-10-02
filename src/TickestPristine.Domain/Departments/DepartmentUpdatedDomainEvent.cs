using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Departments;

public sealed record DepartmentUpdatedDomainEvent(Guid DepartmentId) : IDomainEvent;
