using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace TickestPristine.Infrastructure.Authorization;

internal sealed class ActiveUserAuthorizationHandler(IServiceScopeFactory serviceScopeFactory)
    : AuthorizationHandler<ActiveUserRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveUserRequirement requirement)
    {
        if (context.User is not { Identity.IsAuthenticated: true })
        {
            return;
        }

        using IServiceScope scope = serviceScopeFactory.CreateScope();

        IPermissionProvider permissionProvider = scope.ServiceProvider.GetRequiredService<IPermissionProvider>();

        if (await permissionProvider.IsActiveAsync(context.User.GetUserId()))
        {
            context.Succeed(requirement);
        }
    }
}
