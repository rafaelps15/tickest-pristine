using TickestPristine.Domain.Users;

namespace TickestPristine.Application.Abstractions.Authentication;

public interface ITokenProvider
{
    string Create(User user);

    string GenerateRefreshToken();
}
