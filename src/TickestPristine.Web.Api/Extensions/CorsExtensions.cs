using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Extensions;

internal static class CorsExtensions
{
    internal const string DefaultPolicyName = "Default";

    internal static IServiceCollection AddCorsInternal(this IServiceCollection services)
    {
        services.AddOptions<CorsPolicyOptions>()
            .BindConfiguration(CorsPolicyOptions.SectionName);

        services.AddCors();

        // Sem origens configuradas, a política fica vazia e o navegador bloqueia as chamadas de outros domínios.
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<CorsPolicyOptions>>((cors, corsPolicyOptions) =>
            {
                string[] allowedOrigins = corsPolicyOptions.Value.AllowedOrigins;

                cors.AddPolicy(DefaultPolicyName, policy =>
                {
                    if (allowedOrigins.Length > 0)
                    {
                        policy.WithOrigins(allowedOrigins)
                            .AllowAnyHeader()
                            .AllowAnyMethod();
                    }
                });
            });

        return services;
    }
}
