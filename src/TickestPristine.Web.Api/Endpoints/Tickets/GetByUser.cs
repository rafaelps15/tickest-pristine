using TickestPristine.Application.Abstractions.Messaging;
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
            IQueryHandler<GetTicketsByUserQuery, List<TicketResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTicketsByUserQuery(userId);

            Result<List<TicketResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Tickets);
    }
}
