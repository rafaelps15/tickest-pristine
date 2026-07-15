using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.Delete;

public sealed record DeleteUserCommand(Guid UserId) : ICommand;
