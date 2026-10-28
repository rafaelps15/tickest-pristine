using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;

namespace TickestPristine.Application.Users.GetAll;

/// <summary>
/// Lista os usuários com filtros opcionais. O período de cadastro inclui os dois dias informados. Com
/// <see cref="WithoutRole"/>, só os usuários sem nenhuma role, e o <see cref="RoleName"/> é ignorado. Sem
/// <see cref="SortBy"/>, a ordem é por nome; sem <see cref="SortDirection"/>, o nome é crescente e o cadastro, decrescente.
/// </summary>
public sealed record GetUsersQuery(
    UserStatusFilter Status,
    string? Search,
    DateOnly? CreatedFrom,
    DateOnly? CreatedTo,
    string? RoleName,
    bool WithoutRole,
    UserSortField? SortBy,
    SortDirection? SortDirection,
    PageRequest Paging) : IQuery<PagedResponse<UserSummaryResponse>>;
