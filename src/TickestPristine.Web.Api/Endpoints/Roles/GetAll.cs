using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Roles.GetAll;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Roles;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("roles", async (
            IQueryHandler<GetAllRolesQuery, List<RoleResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<List<RoleResponse>> result = await handler.Handle(new GetAllRolesQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Roles.Manage)
        .WithTags(Tags.Roles);
    }
}
