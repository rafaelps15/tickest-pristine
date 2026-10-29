using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Roles.Create;

public sealed class CreateRoleCommand : ICommand<RoleResponse>
{
    // RoleName, não Name: é o que dá o rótulo "Função" às mensagens de validação (ValidationDisplayNames).
    public string RoleName { get; set; }
}
