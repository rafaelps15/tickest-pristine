using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Departments.Create;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Departments;

internal sealed class Create : IEndpoint
{
    public sealed class Request
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Guid? ResponsibleUserId { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("departments", async (
            Request request,
            ICommandHandler<CreateDepartmentCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateDepartmentCommand
            {
                Name = request.Name,
                Description = request.Description,
                ResponsibleUserId = request.ResponsibleUserId
            };

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Departments.Manage)
        .WithTags(Tags.Departments);
    }
}
