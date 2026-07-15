using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Todos;

public sealed record TodoItemDeletedDomainEvent(Guid TodoItemId) : IDomainEvent;
