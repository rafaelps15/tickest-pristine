using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.Deactivate;

public sealed record DeactivateUserCommand(Guid UserId) : ICommand;
