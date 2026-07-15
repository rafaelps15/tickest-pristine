using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.Register;

public sealed record RegisterUserCommand(string Email, string FirstName, string LastName, string Password)
    : ICommand<Guid>;
