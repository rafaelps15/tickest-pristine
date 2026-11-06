using System.Data.Common;
using Dapper;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Domain.Departments;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Departments.GetById;

internal sealed class GetDepartmentByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetDepartmentByIdQuery, DepartmentResponse>
{
    public async Task<Result<DepartmentResponse>> Handle(
        GetDepartmentByIdQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync(cancellationToken);

        const string sql =
            $"""
             SELECT
                 id AS {nameof(DepartmentResponse.Id)},
                 name AS {nameof(DepartmentResponse.Name)},
                 description AS {nameof(DepartmentResponse.Description)},
                 is_active AS {nameof(DepartmentResponse.IsActive)},
                 responsible_user_id AS {nameof(DepartmentResponse.ResponsibleUserId)}
             FROM public.departments
             WHERE id = @DepartmentId
             """;

        DepartmentResponse? department = await connection.QuerySingleOrDefaultAsync<DepartmentResponse>(
            new CommandDefinition(sql, request, cancellationToken: cancellationToken));

        if (department is null)
        {
            return Result.Failure<DepartmentResponse>(DepartmentErrors.NotFound(request.DepartmentId));
        }

        return department;
    }
}
