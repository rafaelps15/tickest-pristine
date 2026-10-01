using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.Application.Users.GetAll;

public sealed record GetUsersQuery : IQuery<List<UserSummaryResponse>>;
