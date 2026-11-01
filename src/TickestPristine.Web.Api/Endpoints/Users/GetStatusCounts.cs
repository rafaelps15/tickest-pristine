using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Users.GetStatusCounts;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal sealed class GetStatusCounts : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users/stats/status", async (
            IQueryHandler<GetUserStatusCountsQuery, UserStatusCountsResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetUserStatusCountsQuery();

            Result<UserStatusCountsResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .HasPermission(Permissions.UsersRead);
    }
}
