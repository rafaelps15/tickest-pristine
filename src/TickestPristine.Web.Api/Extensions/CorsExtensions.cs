namespace TickestPristine.Web.Api.Extensions;

internal static class CorsExtensions
{
    internal const string DefaultPolicyName = "Default";

    internal static IServiceCollection AddCorsInternal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(DefaultPolicyName, policy =>
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
