using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.GetAll;

public sealed record GetAllUsersQuery : IQuery<List<UserSummaryResponse>>;
