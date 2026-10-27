using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.GetById;

internal sealed class GetUserByIdQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IPermissionProvider permissionProvider)
    : IQueryHandler<GetUserByIdQuery, UserResponse>
{
    public async Task<Result<UserResponse>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        bool canReadUsers = await permissionProvider.IsSelfOrHasPermissionAsync(
            userContext.UserId,
            query.UserId,
            PermissionCodes.Users.Read,
            cancellationToken);

        if (!canReadUsers)
        {
            return Result.Failure<UserResponse>(UserErrors.Unauthorized());
        }

        UserResponse? user = await context.Users
            .Where(u => u.Id == query.UserId)
            .Select(u => new UserResponse
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                // Setor opcional: sem setor, as subconsultas não encontram nada e os campos vêm nulos; o setor
                // desativado continua aparecendo, com SectorIsActive = false.
                SectorId = u.SectorId,
                SectorName = context.Sectors
                    .Where(s => s.Id == u.SectorId)
                    .Select(s => s.Name)
                    .FirstOrDefault(),
                DepartmentName = context.Departments
                    .Where(d => context.Sectors.Any(s => s.Id == u.SectorId && s.DepartmentId == d.Id))
                    .Select(d => d.Name)
                    .FirstOrDefault(),
                SectorIsActive = context.Sectors
                    .Where(s => s.Id == u.SectorId)
                    .Select(s => (bool?)s.IsActive)
                    .FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserResponse>(UserErrors.NotFound(query.UserId));
        }

        return user;
    }
}
