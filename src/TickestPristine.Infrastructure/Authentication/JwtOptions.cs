namespace TickestPristine.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public required string SecurityKey { get; init; }

    public required int AccessTokenExpirationInMinutes { get; init; }

    public required int RefreshTokenExpirationInDays { get; init; }
}
