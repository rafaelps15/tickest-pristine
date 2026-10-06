using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Pagination;
using TickestPristine.Application.Tickets.GetByUser;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class GetByUser : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("tickets/users/{userId:guid}", async (
            Guid userId,
            [AsParameters] PageRequest paging,
            IQueryHandler<GetTicketsByUserQuery, PagedResponse<TicketResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketsByUserQuery(userId, paging);

            Result<PagedResponse<TicketResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .RequireAuthorization();
    }
}
