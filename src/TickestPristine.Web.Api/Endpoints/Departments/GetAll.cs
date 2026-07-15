using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Departments.GetAll;
using TickestPristine.SharedKernel;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Endpoints.Departments;

internal sealed class GetAll : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("departments", async (
            IQueryHandler<GetDepartmentsQuery, List<DepartmentResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<List<DepartmentResponse>> result = await handler.Handle(new GetDepartmentsQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Departments);
    }
}
