using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.GetCurrent;

internal sealed class GetCurrentUserQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    public async Task<Result<CurrentUserResponse>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        Guid userId = userContext.UserId;

        var user = await context.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.IsActive,
                Response = new CurrentUserResponse
                {
                    Id = u.Id,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Roles = context.Roles
                        .Where(r => context.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == r.Id))
                        .OrderBy(r => r.Name)
                        .Select(r => new CurrentUserRoleResponse { Id = r.Id, Name = r.Name })
                        .ToList()
                }
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Result.Failure<CurrentUserResponse>(UserErrors.NotFound(userId));
        }

        // Mesma regra do login e do refresh: o access token ainda vale, mas a conta não pode mais ser usada.
        if (!user.IsActive)
        {
            return Result.Failure<CurrentUserResponse>(UserErrors.Deactivated);
        }

        // Mesma fonte que a autorização usa: o front mostra exatamente o que a API vai permitir.
        HashSet<string> permissions = await permissionProvider.GetForUserIdAsync(userId, cancellationToken);

        return user.Response with { Permissions = [.. permissions.Order(StringComparer.Ordinal)] };
    }
}
