namespace TickestPristine.Infrastructure.Authorization;

internal static class PermissionCacheKeys
{
    internal static string ForUser(Guid userId) => $"permissions-{userId}";

    internal static string ActiveForUser(Guid userId) => $"user-active-{userId}";
}
