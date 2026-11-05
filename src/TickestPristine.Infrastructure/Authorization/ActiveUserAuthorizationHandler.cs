using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace TickestPristine.Infrastructure.Authorization;

internal sealed class ActiveUserAuthorizationHandler(IPermissionProvider permissionProvider)
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

        // Na autorização das rotas, o recurso é o HttpContext da requisição.
        CancellationToken cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;

        if (await permissionProvider.IsActiveAsync(context.User.GetUserId(), cancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}
