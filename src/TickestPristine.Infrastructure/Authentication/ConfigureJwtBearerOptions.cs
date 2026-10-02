using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace TickestPristine.Infrastructure.Authentication;

/// <summary>
/// Configura a validação do JWT (esquema Bearer) a partir de <see cref="JwtOptions"/>.
/// </summary>
internal sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        JwtOptions jwt = jwtOptions.Value;

        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = jwt.CreateSigningKey(),
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            ClockSkew = TimeSpan.Zero
        };
    }
}
