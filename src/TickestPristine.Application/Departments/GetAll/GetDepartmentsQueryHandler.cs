using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Departments.GetAll;

internal sealed class GetDepartmentsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetDepartmentsQuery, List<DepartmentResponse>>
{
    public async Task<Result<List<DepartmentResponse>>> Handle(GetDepartmentsQuery query, CancellationToken cancellationToken)
    {
        List<DepartmentResponse> departments = await context.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentResponse
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                ResponsibleUserName = context.Users
                    .Where(u => u.Id == d.ResponsibleUserId)
                    .Select(u => u.FirstName + " " + u.LastName)
                    .SingleOrDefault(),
                Sectors = context.Sectors
                    .Where(s => s.DepartmentId == d.Id && s.IsActive)
                    .OrderBy(s => s.Name)
                    .Select(s => new DepartmentSectorResponse { Id = s.Id, Name = s.Name })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return departments;
    }
}
