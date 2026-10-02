using TickestPristine.Domain.Users;

namespace TickestPristine.Application.Abstractions.Authentication;

public interface ITokenProvider
{
    Task<string> CreateAsync(User user, CancellationToken cancellationToken = default);

    string GenerateRefreshToken();
}
