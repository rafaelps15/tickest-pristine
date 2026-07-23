using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Users.GetAll;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Users;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users", async (
            IQueryHandler<GetAllUsersQuery, List<UserSummaryResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<List<UserSummaryResponse>> result = await handler.Handle(new GetAllUsersQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Users.Manage)
        .WithTags(Tags.Users);
    }
}
