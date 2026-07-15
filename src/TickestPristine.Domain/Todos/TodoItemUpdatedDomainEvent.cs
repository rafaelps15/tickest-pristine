using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Todos;

public sealed record TodoItemUpdatedDomainEvent(Guid TodoItemId) : IDomainEvent;
