using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AccessTokensResponse>;
