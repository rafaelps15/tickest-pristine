using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Todos.Get;

public sealed record GetTodosQuery(Guid UserId) : IQuery<List<TodoResponse>>;
