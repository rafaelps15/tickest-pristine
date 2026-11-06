using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Departments.GetAll;

internal sealed class GetDepartmentsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetDepartmentsQuery, List<DepartmentResponse>>
{
    public async Task<Result<List<DepartmentResponse>>> Handle(
        GetDepartmentsQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        // As contagens usam o mesmo critério e o mesmo alcance das recusas de desativação, para que zero signifique
        // desativação aceita: no setor, os chamados ativos dele; no departamento, os de todos os setores dele, inclusive
        // os desativados. Uma linha por setor ativo, com o departamento repetido; sem setor ativo, a linha vem sem setor.
        const string sql =
            $"""
             SELECT
                 d.id AS {nameof(DepartmentResponse.Id)},
                 d.name AS {nameof(DepartmentResponse.Name)},
                 d.description AS {nameof(DepartmentResponse.Description)},
                 d.responsible_user_id AS {nameof(DepartmentResponse.ResponsibleUserId)},
                 (SELECT ru.first_name || ' ' || ru.last_name
                  FROM public.users ru
                  WHERE ru.id = d.responsible_user_id) AS {nameof(DepartmentResponse.ResponsibleUserName)},
                 (SELECT COUNT(*)::int
                  FROM public.tickets t
                  JOIN public.sectors ts ON ts.id = t.sector_id
                  WHERE ts.department_id = d.id
                    AND t.deleted_at_utc IS NULL
                    AND {TicketSql.IsActive}) AS {nameof(DepartmentResponse.ActiveTicketCount)},
                 s.id AS {nameof(DepartmentSectorResponse.Id)},
                 s.name AS {nameof(DepartmentSectorResponse.Name)},
                 s.description AS {nameof(DepartmentSectorResponse.Description)},
                 (SELECT COUNT(*)::int
                  FROM public.tickets t
                  WHERE t.sector_id = s.id
                    AND t.deleted_at_utc IS NULL
                    AND {TicketSql.IsActive}) AS {nameof(DepartmentSectorResponse.ActiveTicketCount)}
             FROM public.departments d
             LEFT JOIN public.sectors s ON s.department_id = d.id AND s.is_active
             WHERE d.is_active
             ORDER BY d.name, d.id, s.name
             """;

        var departments = new List<DepartmentResponse>();

        await connection.QueryAsync<DepartmentResponse, DepartmentSectorResponse?, DepartmentResponse>(
            new CommandDefinition(sql, cancellationToken: cancellationToken),
            (department, sector) =>
            {
                if (departments.Count == 0 || departments[^1].Id != department.Id)
                {
                    departments.Add(department);
                }

                if (sector is not null)
                {
                    departments[^1].Sectors.Add(sector);
                }

                return department;
            },
            splitOn: nameof(DepartmentSectorResponse.Id));

        return departments;
    }
}
