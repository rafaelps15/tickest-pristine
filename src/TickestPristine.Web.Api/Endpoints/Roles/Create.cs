using TickestPristine.Application.Abstractions.Messaging;
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
            ICommandHandler<CreateRoleCommand, RoleResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateRoleCommand { RoleName = request.Name };

            Result<RoleResponse> result = await handler.Handle(command, cancellationToken);

            // 201 com o endereço da consulta pelo id (spec 036, AC-1).
            return result.Match<RoleResponse, IResult>(
                role => Results.Created($"/roles/{role.Id}", role),
                CustomResults.Problem);
        })
        .WithTags(Tags.Roles)
        .HasPermission(Permissions.RolesManage);
    }
}
