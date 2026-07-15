using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Todos;

public sealed record TodoItemCompletedDomainEvent(Guid TodoItemId) : IDomainEvent;
