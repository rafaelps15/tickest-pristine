using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Todos.GetById;

public sealed record GetTodoByIdQuery(Guid TodoItemId) : IQuery<TodoResponse>;
