using Microsoft.AspNetCore.Mvc;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets;
using TickestPristine.Application.Tickets.GetVolume;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class GetVolume : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Os filtros de recorte de GET tickets; createdFrom e createdTo definem o intervalo do gráfico (spec 028). Exige só
        // usuário ativo: com filtro de recorte, o handler confere tickets:manage, para responder 403 com mensagem de
        // negócio (spec 031).
        app.MapGet("tickets/stats/volume", async (
            TicketVolumeGrouping? groupBy,
            [FromQuery(Name = "sectorId")] Guid[]? sectorIds,
            TicketPriority? priority,
            Guid? requesterId,
            Guid? assignedToUserId,
            bool? unassignedOnly,
            DateOnly? createdFrom,
            DateOnly? createdTo,
            IQueryHandler<GetTicketVolumeQuery, List<TicketVolumeResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            var scope = new TicketScopeFilter(
                sectorIds ?? [],
                priority,
                requesterId,
                assignedToUserId,
                unassignedOnly ?? false,
                null,
                null,
                null,
                null);
            var query = new GetTicketVolumeQuery(groupBy ?? TicketVolumeGrouping.Day, scope, createdFrom, createdTo);

            Result<List<TicketVolumeResponse>> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Tickets)
        .RequireAuthorization();
    }
}
