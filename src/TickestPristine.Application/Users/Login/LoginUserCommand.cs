using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.Login;

public sealed record LoginUserCommand(string Email, string Password) : ICommand<AccessTokensResponse>;
