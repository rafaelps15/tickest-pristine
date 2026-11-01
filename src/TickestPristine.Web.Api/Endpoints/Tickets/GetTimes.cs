using Microsoft.AspNetCore.Mvc;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets;
using TickestPristine.Application.Tickets.GetTimes;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class GetTimes : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Os mesmos filtros de recorte de GET tickets; o período de resolução vale para a data de cada conclusão.
        app.MapGet("tickets/stats/times", async (
            [FromQuery(Name = "sectorId")] Guid[]? sectorIds,
            TicketPriority? priority,
            Guid? requesterId,
            Guid? assignedToUserId,
            bool? unassignedOnly,
            DateOnly? createdFrom,
            DateOnly? createdTo,
            DateOnly? resolvedFrom,
            DateOnly? resolvedTo,
            IQueryHandler<GetTicketTimesQuery, TicketTimesResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var scope = new TicketScopeFilter(
                sectorIds ?? [],
                priority,
                requesterId,
                assignedToUserId,
                unassignedOnly ?? false,
                createdFrom,
                createdTo,
                resolvedFrom,
                resolvedTo);
            var query = new GetTicketTimesQuery(scope);

            Result<TicketTimesResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .HasPermission(Permissions.TicketsManage);
    }
}
