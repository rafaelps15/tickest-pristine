using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Users.ChangePassword;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal sealed class ChangePassword : IEndpoint
{
    public sealed record Request(string CurrentPassword, string NewPassword);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/me/password", async (
            Request request,
            ICommandHandler<ChangeUserPasswordCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ChangeUserPasswordCommand(request.CurrentPassword, request.NewPassword);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .RequireAuthorization()
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
