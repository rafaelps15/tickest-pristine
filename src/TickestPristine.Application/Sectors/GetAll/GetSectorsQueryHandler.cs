using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Sectors.GetAll;

internal sealed class GetSectorsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetSectorsQuery, List<SectorResponse>>
{
    public async Task<Result<List<SectorResponse>>> Handle(GetSectorsQuery query, CancellationToken cancellationToken)
    {
        List<SectorResponse> sectors = await context.Sectors
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new SectorResponse
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                DepartmentId = s.DepartmentId
            })
            .ToListAsync(cancellationToken);

        return sectors;
    }
}
