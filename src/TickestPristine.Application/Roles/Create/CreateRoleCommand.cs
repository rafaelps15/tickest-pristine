using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Roles.Create;

public sealed class CreateRoleCommand : ICommand<Guid>
{
    public string Name { get; set; }
}
