using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Todos.Update;

public sealed record UpdateTodoCommand(
    Guid TodoItemId,
    string Description) : ICommand;
