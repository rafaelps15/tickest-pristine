using TickestPristine.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;

namespace TickestPristine.Infrastructure.Authentication;

internal sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public Guid UserId => httpContextAccessor.HttpContext?.User.GetUserId() ?? throw new UserContextUnavailableException();

    public Guid? UserIdOrDefault => httpContextAccessor.HttpContext?.User.FindUserId();
}
