using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.GetAll;

internal sealed class GetUsersQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetUsersQuery, PagedResponse<UserSummaryResponse>>
{
    public async Task<Result<PagedResponse<UserSummaryResponse>>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
    {
        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var createdFromUtc = query.CreatedFrom?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var createdUntilUtc = query.CreatedTo?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        PagedResponse<UserSummaryResponse> users = await context.Users
            .Where(u =>
                (query.Status == UserStatusFilter.All || u.IsActive == (query.Status == UserStatusFilter.Active)) &&
                (search == null ||
                 u.FirstName.Contains(search) ||
                 u.LastName.Contains(search) ||
                 u.Email.Contains(search)) &&
                (createdFromUtc == null || u.CreatedAtUtc >= createdFromUtc) &&
                (createdUntilUtc == null || u.CreatedAtUtc < createdUntilUtc))
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ThenBy(u => u.Id)
            .Select(u => new UserSummaryResponse
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                IsActive = u.IsActive,
                CreatedAtUtc = u.CreatedAtUtc,
                DeactivatedAtUtc = u.DeactivatedAtUtc,
                Roles = context.Roles
                    .Where(r => context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == r.Id))
                    .Select(r => new RoleSummaryResponse { Id = r.Id, Name = r.Name })
                    .ToList()
            })
            .ToPagedResponseAsync(query.Page, query.PageSize, cancellationToken);

        return users;
    }
}
