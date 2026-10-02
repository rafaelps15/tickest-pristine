using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Extensions;

internal static class RateLimitingExtensions
{
    internal static IServiceCollection AddRateLimitingInternal(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<RateLimitingOptions>, RateLimitingOptionsValidator>();

        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .ValidateOnStart();

        services.AddRateLimiter(_ => { });

        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((limiter, rateLimitingOptions) =>
            {
                RateLimitingOptions limits = rateLimitingOptions.Value;

                limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Limite global por janela fixa, separado por usuário autenticado ou por IP.
                limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: _ => ToLimiterOptions(limits.Global)));

                // Limite mais restrito para os endpoints de autenticação.
                limiter.AddPolicy(RateLimitingPolicies.Authentication, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: _ => ToLimiterOptions(limits.Authentication)));
            });

        return services;
    }

    private static FixedWindowRateLimiterOptions ToLimiterOptions(FixedWindowLimitOptions limit) => new()
    {
        PermitLimit = limit.PermitLimit,
        Window = TimeSpan.FromSeconds(limit.WindowInSeconds)
    };

    private static string GetPartitionKey(HttpContext httpContext)
    {
        return httpContext.User.Identity?.Name
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
    }
}
