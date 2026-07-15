using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Todos.Complete;

public sealed record CompleteTodoCommand(Guid TodoItemId) : ICommand;
