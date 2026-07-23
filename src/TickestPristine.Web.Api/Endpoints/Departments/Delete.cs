using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Departments.Delete;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Departments;

internal sealed class Delete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("departments/{departmentId:guid}", async (
            Guid departmentId,
            ICommandHandler<DeleteDepartmentCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new DeleteDepartmentCommand(departmentId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Departments.Manage)
        .WithTags(Tags.Departments);
    }
}
