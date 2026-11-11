using Microsoft.EntityFrameworkCore;
using TickestPristine.Domain.Roles;

namespace TickestPristine.Application.Roles;

internal static class RoleQueries
{
    public static IQueryable<Role> WithPermissions(IQueryable<Role> roles) => roles.Include(role => role.Permissions);
}
