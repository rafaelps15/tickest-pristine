using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.ChangePassword;

public sealed record ChangeUserPasswordCommand(string CurrentPassword, string NewPassword) : ICommand;
