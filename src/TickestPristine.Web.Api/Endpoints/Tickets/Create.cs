using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Tickets.Create;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Tickets;

internal sealed class Create : IEndpoint
{
    public sealed class Request
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public TicketPriority Priority { get; set; }
        public Guid? RequesterId { get; set; }
        public Guid? ResponsibleId { get; set; }
        public Guid SectorId { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("tickets", async (
            Request request,
            ICommandHandler<CreateTicketCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateTicketCommand
            {
                Title = request.Title,
                Description = request.Description,
                Priority = request.Priority,
                RequesterId = request.RequesterId,
                ResponsibleId = request.ResponsibleId,
                SectorId = request.SectorId
            };

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Tickets.Create)
        .WithTags(Tags.Tickets);
    }
}
