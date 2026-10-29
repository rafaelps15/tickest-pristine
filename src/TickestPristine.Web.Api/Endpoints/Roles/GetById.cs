using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Roles.GetById;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Roles;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("roles/{roleId:guid}", async (
            Guid roleId,
            IQueryHandler<GetRoleByIdQuery, RoleResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetRoleByIdQuery(roleId);

            Result<RoleResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Roles)
        .HasPermission(Permissions.RolesRead);
    }
}
