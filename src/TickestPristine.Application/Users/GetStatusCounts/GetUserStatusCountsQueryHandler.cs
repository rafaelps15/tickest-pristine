using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.Users.GetStatusCounts;

internal sealed class GetUserStatusCountsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetUserStatusCountsQuery, UserStatusCountsResponse>
{
    public async Task<Result<UserStatusCountsResponse>> Handle(
        GetUserStatusCountsQuery query,
        CancellationToken cancellationToken)
    {
        // Um comando só: o banco agrupa por situação e "tem senha" (no máximo quatro linhas), e a soma fica em memória.
        // Conta pendente é o usuário ativo sem credencial (spec 008).
        var groups = await context.Users
            .Select(u => new
            {
                u.IsActive,
                HasCredential = context.UserCredentials.Any(c => c.UserId == u.Id)
            })
            .GroupBy(u => new { u.IsActive, u.HasCredential })
            .Select(g => new { g.Key.IsActive, g.Key.HasCredential, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new UserStatusCountsResponse
        {
            TotalCount = groups.Sum(g => g.Count),
            ActiveCount = groups.Where(g => g.IsActive && g.HasCredential).Sum(g => g.Count),
            PendingActivationCount = groups.Where(g => g.IsActive && !g.HasCredential).Sum(g => g.Count),
            InactiveCount = groups.Where(g => !g.IsActive).Sum(g => g.Count)
        };
    }
}
