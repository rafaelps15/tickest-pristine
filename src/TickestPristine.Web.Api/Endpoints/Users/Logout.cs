using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Users.Logout;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal sealed class Logout : IEndpoint
{
    public sealed record Request(string RefreshToken);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/logout", async (
            Request request,
            ICommandHandler<LogoutUserCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new LogoutUserCommand(request.RefreshToken);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .RequireAuthorization();
    }
}
