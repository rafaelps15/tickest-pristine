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
            async cancellation => await context.UserPermissions
                .Where(p => p.UserId == userId)
                .Select(p => p.PermissionCode)
                .ToListAsync(cancellation),
            cancellationToken: cancellationToken);

        return [.. permissions];
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default)
    {
        HashSet<string> permissions = await GetForUserIdAsync(userId, cancellationToken);

        return permissions.Contains(permission);
    }

    public async Task InvalidateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(PermissionCacheKeys.ForUser(userId), cancellationToken);
    }
}
