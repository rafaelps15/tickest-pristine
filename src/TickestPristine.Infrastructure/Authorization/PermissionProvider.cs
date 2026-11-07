using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace TickestPristine.Infrastructure.Authorization;

internal sealed class PermissionProvider(ApplicationDbContext context, HybridCache cache) : IPermissionProvider
{
    public async Task<HashSet<string>> GetForUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        List<string> permissions = await cache.GetOrCreateAsync(
            PermissionCacheKeys.ForUser(userId),
            async cancellation => await ActiveUserPermissions(userId).ToListAsync(cancellation),
            cancellationToken: cancellationToken);

        return [.. permissions];
    }

    public async Task<HashSet<string>> GetCurrentForUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        [.. await ActiveUserPermissions(userId).ToListAsync(cancellationToken)];

    public async Task<HashSet<string>> GetGrantedByRolesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        [.. await context.UserPermissions
            .Where(permission => permission.UserId == userId)
            .Select(permission => permission.PermissionCode)
            .ToListAsync(cancellationToken)];

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

    // A união das funções vem da view; aqui entra só a situação do usuário, que a view não olha.
    private IQueryable<string> ActiveUserPermissions(Guid userId) =>
        from permission in context.UserPermissions
        where permission.UserId == userId
        join user in context.Users on permission.UserId equals user.Id
        where user.IsActive
        select permission.PermissionCode;
}
