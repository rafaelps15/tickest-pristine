using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.GetAll;

internal sealed class GetAllUsersQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAllUsersQuery, List<UserSummaryResponse>>
{
    public async Task<Result<List<UserSummaryResponse>>> Handle(GetAllUsersQuery query, CancellationToken cancellationToken)
    {
        List<UserSummaryResponse> users = await context.Users
            .OrderBy(u => u.FirstName)
            .Select(u => new UserSummaryResponse
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName
            })
            .ToListAsync(cancellationToken);

        return users;
    }
}
