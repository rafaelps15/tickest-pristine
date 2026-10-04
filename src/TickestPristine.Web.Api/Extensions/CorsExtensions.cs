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
                CorsPolicyOptions options = corsPolicyOptions.Value;

                // O navegador manda a origem sem barra no final; a configuração pode ter sido escrita com ela.
                string[] allowedOrigins = [.. options.AllowedOrigins.Select(origin => origin.TrimEnd('/'))];

                cors.AddPolicy(DefaultPolicyName, policy =>
                {
                    if (options.AllowLoopbackOrigins)
                    {
                        policy.SetIsOriginAllowed(origin => IsLoopbackOrigin(origin) || allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                            .AllowAnyHeader()
                            .AllowAnyMethod();
                    }
                    else if (allowedOrigins.Length > 0)
                    {
                        policy.WithOrigins(allowedOrigins)
                            .AllowAnyHeader()
                            .AllowAnyMethod();
                    }
                });
            });

        return services;
    }

    private static bool IsLoopbackOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) && uri.IsLoopback;
}
