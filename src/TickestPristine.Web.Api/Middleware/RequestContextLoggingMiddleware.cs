using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace TickestPristine.Web.Api.Middleware;

public class RequestContextLoggingMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeaderName = "Correlation-Id";

    public async Task Invoke(HttpContext context)
    {
        // O await mantém a propriedade no contexto de log até o fim da requisição, e não só até o primeiro await.
        using (LogContext.PushProperty("CorrelationId", GetCorrelationId(context)))
        {
            await next.Invoke(context);
        }
    }

    private static string GetCorrelationId(HttpContext context)
    {
        context.Request.Headers.TryGetValue(
            CorrelationIdHeaderName,
            out StringValues correlationId);

        return correlationId.FirstOrDefault() ?? context.TraceIdentifier;
    }
}
