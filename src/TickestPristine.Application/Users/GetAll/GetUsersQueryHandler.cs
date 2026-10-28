using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Domain.Users;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.GetAll;

internal sealed class GetUsersQueryHandler(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetUsersQuery, PagedResponse<UserSummaryResponse>>
{
    public async Task<Result<PagedResponse<UserSummaryResponse>>> Handle(GetUsersQuery query, CancellationToken cancellationToken)
    {
        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        bool withoutRole = query.WithoutRole;
        string? roleName = withoutRole || string.IsNullOrWhiteSpace(query.RoleName) ? null : query.RoleName.Trim();
        var createdFromUtc = query.CreatedFrom?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var createdUntilUtc = query.CreatedTo?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime nowUtc = dateTimeProvider.UtcNow;

        IQueryable<User> filtered = context.Users
            .Where(u =>
                (query.Status == UserStatusFilter.All || u.IsActive == (query.Status == UserStatusFilter.Active)) &&
                (search == null ||
                 u.FirstName.Contains(search) ||
                 u.LastName.Contains(search) ||
                 u.Email.Contains(search)) &&
                (createdFromUtc == null || u.CreatedAtUtc >= createdFromUtc) &&
                (createdUntilUtc == null || u.CreatedAtUtc < createdUntilUtc) &&
                (!withoutRole || !context.UserRoles.Any(ur => ur.UserId == u.Id)) &&
                (roleName == null || context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleName == roleName)));

        PagedResponse<UserSummaryResponse> users = await Sort(filtered, query.SortBy ?? UserSortField.Name, query.SortDirection)
            .Select(u => new UserSummaryResponse
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                IsActive = u.IsActive,
                CreatedAtUtc = u.CreatedAtUtc,
                DeactivatedAtUtc = u.DeactivatedAtUtc,
                // Setor opcional: sem setor, as subconsultas não encontram nada e os campos vêm nulos; o setor
                // desativado continua aparecendo, com SectorIsActive = false.
                SectorId = u.SectorId,
                SectorName = context.Sectors
                    .Where(s => s.Id == u.SectorId)
                    .Select(s => s.Name)
                    .FirstOrDefault(),
                DepartmentName = context.Departments
                    .Where(d => context.Sectors.Any(s => s.Id == u.SectorId && s.DepartmentId == d.Id))
                    .Select(d => d.Name)
                    .FirstOrDefault(),
                SectorIsActive = context.Sectors
                    .Where(s => s.Id == u.SectorId)
                    .Select(s => (bool?)s.IsActive)
                    .FirstOrDefault(),
                // Conta pendente de ativação (spec 008): usuário ativo que ainda não definiu a senha. A expiração é a do
                // convite ainda utilizável; nula quando ele expirou ou foi revogado.
                PendingActivation = u.IsActive && !context.UserCredentials.Any(c => c.UserId == u.Id),
                InvitationExpiresAtUtc = u.IsActive && !context.UserCredentials.Any(c => c.UserId == u.Id)
                    ? context.UserInvitations
                        .Where(i => i.UserId == u.Id && i.UsedAtUtc == null && i.RevokedAtUtc == null && i.ExpiresAtUtc > nowUtc)
                        .Max(i => (DateTime?)i.ExpiresAtUtc)
                    : null,
                Roles = context.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Select(ur => new RoleSummaryResponse { Name = ur.RoleName })
                    .ToList()
            })
            .ToPagedListAsync(query.Paging, cancellationToken);

        return users;
    }

    // O Id desempata as chaves iguais, para a paginação não repetir nem pular usuários; a direção vale para todas as chaves.
    private static IOrderedQueryable<User> Sort(IQueryable<User> users, UserSortField sortBy, SortDirection? direction)
    {
        if (sortBy == UserSortField.CreatedAt)
        {
            return (direction ?? SortDirection.Descending) == SortDirection.Ascending
                ? users.OrderBy(u => u.CreatedAtUtc).ThenBy(u => u.Id)
                : users.OrderByDescending(u => u.CreatedAtUtc).ThenByDescending(u => u.Id);
        }

        return (direction ?? SortDirection.Ascending) == SortDirection.Ascending
            ? users.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ThenBy(u => u.Id)
            : users.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName).ThenByDescending(u => u.Id);
    }
}
