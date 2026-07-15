using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Todos;

namespace TickestPristine.Application.Todos.Create;

public sealed class CreateTodoCommand : ICommand<Guid>
{
    public Guid UserId { get; set; }
    public string Description { get; set; }
    public DateTime? DueDate { get; set; }
    public List<string> Labels { get; set; } = [];
    public Priority Priority { get; set; }
}
