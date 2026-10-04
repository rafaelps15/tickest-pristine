using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.ChangeRequiredPassword;

public sealed record ChangeRequiredUserPasswordCommand(string Email, string CurrentPassword, string NewPassword)
    : ICommand<AccessTokensResponse>;
