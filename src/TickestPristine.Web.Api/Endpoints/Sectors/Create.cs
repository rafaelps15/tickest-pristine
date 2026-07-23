using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Sectors.Create;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Sectors;

internal sealed class Create : IEndpoint
{
    public sealed class Request
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public Guid DepartmentId { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("sectors", async (
            Request request,
            ICommandHandler<CreateSectorCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateSectorCommand
            {
                Name = request.Name,
                Description = request.Description,
                DepartmentId = request.DepartmentId
            };

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Sectors.Manage)
        .WithTags(Tags.Sectors);
    }
}
