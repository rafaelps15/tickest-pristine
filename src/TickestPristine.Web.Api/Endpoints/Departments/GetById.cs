using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Departments.GetById;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Departments;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("departments/{departmentId:guid}", async (
            Guid departmentId,
            IQueryHandler<GetDepartmentByIdQuery, DepartmentResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var query = new GetDepartmentByIdQuery(departmentId);

            Result<DepartmentResponse> result = await handler.Handle(query, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Departments);
    }
}
