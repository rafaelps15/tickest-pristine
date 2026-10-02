using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TickestPristine.Infrastructure.Authentication;

/// <summary>
/// Configuração da seção "Jwt": chave de assinatura, emissor, audiência e validade do access token.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Chave do HMAC-SHA256: pelo menos 32 caracteres (256 bits).
    /// </summary>
    [Required]
    [MinLength(32)]
    public string Secret { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int ExpirationInMinutes { get; init; }

    /// <summary>
    /// Chave usada tanto para assinar quanto para validar o token.
    /// </summary>
    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(Secret));
}
