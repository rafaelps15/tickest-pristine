using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.Activate;

public sealed record ActivateUserCommand(Guid UserId) : ICommand;
