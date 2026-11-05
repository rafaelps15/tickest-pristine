using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TickestPristine.Web.Api.Infrastructure;

namespace TickestPristine.Web.Api.Extensions;

internal static class RateLimitingExtensions
{
    private const string TooManyRequestsCode = "General.TooManyRequests";

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
                limiter.OnRejected = WriteRejectionAsync;

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

                // Envio e exclusão de anexos: gravam e apagam arquivos, então têm limite próprio, além do global.
                limiter.AddPolicy(RateLimitingPolicies.AttachmentUpload, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: _ => ToLimiterOptions(limits.AttachmentUpload)));

                limiter.AddPolicy(RateLimitingPolicies.AttachmentDelete, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: GetPartitionKey(httpContext),
                        factory: _ => ToLimiterOptions(limits.AttachmentDelete)));
            });

        return services;
    }

    /// <summary>
    /// Responde a recusa do limite em Problem Details, com o código <c>General.TooManyRequests</c> e, quando o limitador
    /// informa, o header <c>Retry-After</c> com os segundos até a janela seguinte.
    /// </summary>
    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        HttpResponse response = context.HttpContext.Response;
        int? retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)
            ? (int)Math.Ceiling(retryAfter.TotalSeconds)
            : null;

        if (retryAfterSeconds is { } seconds)
        {
            response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Type = "https://tools.ietf.org/html/rfc6585#section-4",
            Title = "Muitas requisições",
            Detail = retryAfterSeconds is { } wait
                ? $"O limite de requisições foi atingido. Tente de novo em {wait} segundos."
                : "O limite de requisições foi atingido. Tente de novo em instantes."
        };
        problemDetails.Extensions["code"] = TooManyRequestsCode;

        await response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json", cancellationToken);
    }

    private static FixedWindowRateLimiterOptions ToLimiterOptions(FixedWindowLimitOptions limit) => new()
    {
        PermitLimit = limit.PermitLimit,
        Window = TimeSpan.FromSeconds(limit.WindowInSeconds)
    };

    private static string GetPartitionKey(HttpContext httpContext)
    {
        // Particiona pelo Id do usuário: Identity.Name seria o nome exibido, que pode se repetir entre usuários.
        return httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
    }
}
