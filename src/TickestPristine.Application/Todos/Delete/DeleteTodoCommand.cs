using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Todos.Delete;

public sealed record DeleteTodoCommand(Guid TodoItemId) : ICommand;
