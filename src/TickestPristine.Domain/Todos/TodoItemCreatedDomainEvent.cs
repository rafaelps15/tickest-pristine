using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Todos;

public sealed record TodoItemCreatedDomainEvent(Guid TodoItemId) : IDomainEvent;
