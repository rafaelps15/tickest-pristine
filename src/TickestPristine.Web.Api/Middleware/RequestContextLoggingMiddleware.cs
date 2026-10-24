using System.Diagnostics;
using System.Text.RegularExpressions;
using Serilog.Context;

namespace TickestPristine.Web.Api.Middleware;

public partial class RequestContextLoggingMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeaderName = "Correlation-Id";

    public async Task Invoke(HttpContext context)
    {
        string correlationId = GetCorrelationId(context);

        // Devolvido na resposta para que o cliente consiga relacionar a chamada com os logs do servidor. Gravado no
        // OnStarting porque o UseExceptionHandler limpa os headers antes de escrever o ProblemDetails.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;

            return Task.CompletedTask;
        });

        // O await mantém a propriedade no contexto de log até o fim da requisição, e não só até o primeiro await.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next.Invoke(context);
        }
    }

    private static string GetCorrelationId(HttpContext context)
    {
        string? correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault();

        // O valor vem do cliente e vai para os logs: só é aceito se for curto e sem caracteres fora do formato.
        if (correlationId is not null && CorrelationIdFormat().IsMatch(correlationId))
        {
            return correlationId;
        }

        // Sem um valor aceito, usa o trace do OpenTelemetry, para o log bater com o trace da mesma requisição.
        return Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex CorrelationIdFormat();
}
