using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Departments.Update;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Departments;

internal sealed class Update : IEndpoint
{
    public sealed class Request
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Guid? ResponsibleUserId { get; set; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("departments/{departmentId:guid}", async (
            Guid departmentId,
            Request request,
            ICommandHandler<UpdateDepartmentCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateDepartmentCommand
            {
                DepartmentId = departmentId,
                Name = request.Name,
                Description = request.Description,
                ResponsibleUserId = request.ResponsibleUserId
            };

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Departments.Manage)
        .WithTags(Tags.Departments);
    }
}
