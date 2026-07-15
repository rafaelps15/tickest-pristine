using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Permissions.GetAll;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Permissions;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("permissions", async (
            IQueryHandler<GetAllPermissionsQuery, IReadOnlyList<string>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<IReadOnlyList<string>> result = await handler.Handle(new GetAllPermissionsQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Permissions);
    }
}
