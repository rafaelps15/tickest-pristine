using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Departments.GetAll;

internal sealed class GetDepartmentsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetDepartmentsQuery, List<DepartmentResponse>>
{
    public async Task<Result<List<DepartmentResponse>>> Handle(GetDepartmentsQuery query, CancellationToken cancellationToken)
    {
        // As contagens usam o mesmo critério e o mesmo alcance das recusas de desativação, para que zero signifique
        // desativação aceita: no setor, os chamados ativos dele; no departamento, os de todos os setores dele, inclusive
        // os desativados. Elas entram como subconsultas da projeção abaixo, que continua sendo uma única ida ao banco.
        IQueryable<Ticket> activeTickets = context.Tickets.WhereActive();

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
                ActiveTicketCount = activeTickets.Count(t =>
                    context.Sectors.Any(s => s.Id == t.SectorId && s.DepartmentId == d.Id)),
                Sectors = context.Sectors
                    .Where(s => s.DepartmentId == d.Id && s.IsActive)
                    .OrderBy(s => s.Name)
                    .Select(s => new DepartmentSectorResponse
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Description = s.Description,
                        ActiveTicketCount = activeTickets.Count(t => t.SectorId == s.Id)
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return departments;
    }
}
