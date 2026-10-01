using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.Departments.Deactivate;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Departments;

internal sealed class Deactivate : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("departments/{departmentId:guid}", async (
            Guid departmentId,
            ICommandHandler<DeactivateDepartmentCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new DeactivateDepartmentCommand(departmentId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Departments)
        .HasPermission(PermissionCodes.Departments.Manage);
    }
}
