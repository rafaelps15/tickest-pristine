using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace TickestPristine.Infrastructure.Authorization;

internal sealed class PermissionProvider(IApplicationDbContext context, HybridCache cache) : IPermissionProvider
{
    public async Task<HashSet<string>> GetForUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        List<string> permissions = await cache.GetOrCreateAsync(
            PermissionCacheKeys.ForUser(userId),
            async cancellation => await (
                from userRole in context.UserRoles
                where userRole.UserId == userId
                join user in context.Users on userRole.UserId equals user.Id
                where user.IsActive
                join rolePermission in context.RolePermissions on userRole.RoleName equals rolePermission.RoleName
                select rolePermission.PermissionCode)
                .Distinct()
                .ToListAsync(cancellation),
            cancellationToken: cancellationToken);

        return [.. permissions];
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default)
    {
        HashSet<string> permissions = await GetForUserIdAsync(userId, cancellationToken);

        return permissions.Contains(permission);
    }

    /// <summary>
    /// Se o usuário existe e está ativo, em cache junto com as permissões: o <see cref="InvalidateAsync"/> limpa os dois.
    /// </summary>
    public async Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await cache.GetOrCreateAsync(
            PermissionCacheKeys.ActiveForUser(userId),
            async cancellation => await context.Users.AnyAsync(u => u.Id == userId && u.IsActive, cancellation),
            cancellationToken: cancellationToken);

    public async Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(PermissionCacheKeys.ForUser(userId), cancellationToken);
        await cache.RemoveAsync(PermissionCacheKeys.ActiveForUser(userId), cancellationToken);
    }
}
