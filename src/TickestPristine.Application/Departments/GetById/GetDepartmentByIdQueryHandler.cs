using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Departments;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Departments.GetById;

internal sealed class GetDepartmentByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetDepartmentByIdQuery, DepartmentResponse>
{
    public async Task<Result<DepartmentResponse>> Handle(GetDepartmentByIdQuery query, CancellationToken cancellationToken)
    {
        DepartmentResponse? department = await context.Departments
            .Where(d => d.Id == query.DepartmentId)
            .Select(d => new DepartmentResponse
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                IsActive = d.IsActive,
                ResponsibleUserId = d.ResponsibleUserId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (department is null)
        {
            return Result.Failure<DepartmentResponse>(DepartmentErrors.NotFound(query.DepartmentId));
        }

        return department;
    }
}
