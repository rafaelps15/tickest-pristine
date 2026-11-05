namespace TickestPristine.Web.Api.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task Invoke(HttpContext context)
    {
        // Gravado no OnStarting porque o UseExceptionHandler limpa os headers antes de escrever o ProblemDetails.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";

            return Task.CompletedTask;
        });

        return next(context);
    }
}
