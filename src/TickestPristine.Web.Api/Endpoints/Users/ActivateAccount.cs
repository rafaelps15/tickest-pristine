using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Users.ActivateAccount;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal sealed class ActivateAccount : IEndpoint
{
    /// <summary>
    /// Token do link do convite e a senha que o usuário escolheu.
    /// </summary>
    public sealed record Request(string Token, string NewPassword);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Anônima, como o login: quem ativa ainda não tem senha. O limite de requisições é o da autenticação.
        app.MapPost("users/activation", async (
            Request request,
            ICommandHandler<ActivateUserAccountCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ActivateUserAccountCommand(request.Token, request.NewPassword);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingPolicies.Authentication);
    }
}
