using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Users;
using TickestPristine.Application.Users.ChangeRequiredPassword;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal sealed class ChangeRequiredPassword : IEndpoint
{
    public sealed record Request(string Email, string CurrentPassword, string NewPassword);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/login/new-password", async (
            Request request,
            ICommandHandler<ChangeRequiredUserPasswordCommand, AccessTokensResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ChangeRequiredUserPasswordCommand(request.Email, request.CurrentPassword, request.NewPassword);

            Result<AccessTokensResponse> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
