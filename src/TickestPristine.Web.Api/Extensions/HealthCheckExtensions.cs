using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TickestPristine.Infrastructure.Health;

namespace TickestPristine.Web.Api.Extensions;

internal static class HealthCheckExtensions
{
    // health: o PostgreSQL, como sempre; health/live: nenhuma dependência, só o processo; health/ready: banco, pasta de
    // arquivos e, com o Redis configurado, o cache. As três são anônimas. O relatório detalhado (cada checagem, tempos e
    // a mensagem de erro) fica só em Development; nos demais ambientes a resposta é apenas o status.
    internal static WebApplication MapHealthCheckRoutes(this WebApplication app)
    {
        bool detailed = app.Environment.IsDevelopment();

        app.MapHealthChecks("health", OptionsFor(detailed, registration => !IsReadiness(registration)))
            .AllowAnonymous();
        app.MapHealthChecks("health/live", OptionsFor(detailed, _ => false))
            .AllowAnonymous();
        app.MapHealthChecks("health/ready", OptionsFor(detailed, IsReadiness))
            .AllowAnonymous();

        return app;
    }

    private static bool IsReadiness(HealthCheckRegistration registration) =>
        registration.Tags.Contains(HealthCheckTags.Ready);

    private static HealthCheckOptions OptionsFor(bool detailed, Func<HealthCheckRegistration, bool> predicate)
    {
        var options = new HealthCheckOptions { Predicate = predicate };

        if (detailed)
        {
            options.ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse;
        }

        return options;
    }
}
