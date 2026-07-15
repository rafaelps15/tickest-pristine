using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.GetById;

public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserResponse>;
