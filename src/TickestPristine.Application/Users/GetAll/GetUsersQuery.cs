using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;

namespace TickestPristine.Application.Users.GetAll;

/// <summary>
/// Lista os usuários com filtros opcionais. O período de cadastro inclui os dois dias informados.
/// </summary>
public sealed record GetUsersQuery(
    UserStatusFilter Status,
    string? Search,
    DateOnly? CreatedFrom,
    DateOnly? CreatedTo,
    int? Page,
    int? PageSize) : IQuery<PagedResponse<UserSummaryResponse>>;
