using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Todos.Copy;

public sealed class CopyTodoCommand : ICommand<Guid>
{
    public Guid UserId { get; set; }
    public Guid TodoId { get; set; }
}
