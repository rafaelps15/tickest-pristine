using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.GetByEmail;

public sealed record GetUserByEmailQuery(string Email) : IQuery<UserResponse>;
