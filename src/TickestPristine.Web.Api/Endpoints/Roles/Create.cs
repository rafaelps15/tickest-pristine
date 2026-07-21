using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Roles.Create;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Roles;

internal sealed class Create : IEndpoint
{
    public sealed record Request(string Name);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("roles", async (
            Request request,
            ICommandHandler<CreateRoleCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateRoleCommand { Name = request.Name };

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Roles.Manage)
        .WithTags(Tags.Roles);
    }
}
