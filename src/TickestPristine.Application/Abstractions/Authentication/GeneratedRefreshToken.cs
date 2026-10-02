namespace TickestPristine.Application.Abstractions.Authentication;

/// <summary>
/// Refresh token recém-gerado e a data em que ele expira, conforme a configuração da seção "Jwt".
/// </summary>
public sealed record GeneratedRefreshToken(string Token, DateTime ExpiresOnUtc);
