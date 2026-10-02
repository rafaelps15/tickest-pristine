using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.Logout;

public sealed record LogoutUserCommand(string RefreshToken) : ICommand;
