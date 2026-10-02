namespace TickestPristine.Infrastructure.Authorization;

internal static class PermissionCacheKeys
{
    internal static string ForUser(Guid userId) => $"permissions-{userId}";
}
