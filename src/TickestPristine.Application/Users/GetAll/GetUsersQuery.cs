using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;

namespace TickestPristine.Application.Users.GetAll;

/// <summary>
/// Lista os usuários com filtros opcionais. O período de cadastro inclui os dois dias informados. Com
/// <see cref="WithoutRole"/>, só os usuários sem nenhuma role, e o <see cref="RoleName"/> é ignorado.
/// </summary>
public sealed record GetUsersQuery(
    UserStatusFilter Status,
    string? Search,
    DateOnly? CreatedFrom,
    DateOnly? CreatedTo,
    string? RoleName,
    bool WithoutRole,
    PageRequest Paging) : IQuery<PagedResponse<UserSummaryResponse>>;
