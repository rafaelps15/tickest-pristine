using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
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
            UserStatusFilter? status,
            string? search,
            DateOnly? createdFrom,
            DateOnly? createdTo,
            int? page,
            int? pageSize,
            IQueryHandler<GetUsersQuery, PagedResponse<UserSummaryResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetUsersQuery(
                status ?? UserStatusFilter.All,
                search,
                createdFrom,
                createdTo,
                page,
                pageSize);

            Result<PagedResponse<UserSummaryResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Users)
        .HasPermission(PermissionCodes.Users.Read);
    }
}
