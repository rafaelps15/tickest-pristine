using System.Reflection;
using TickestPristine.Application;
using HealthChecks.UI.Client;
using TickestPristine.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using TickestPristine.Web.Api;
using TickestPristine.Web.Api.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) => loggerConfig.ReadFrom.Configuration(context.Configuration));

builder.Services.AddSwaggerGenWithAuth();

builder.Services
    .AddApplication()
    .AddPresentation()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddObservability(builder.Configuration, builder.Environment.ApplicationName);

builder.Services.AddRateLimitingInternal();

builder.Services.AddCorsInternal();

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

WebApplication app = builder.Build();

app.MapEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerWithUi();

    app.ApplyMigrations();
}

// O relatório detalhado (cada checagem, tempos e a mensagem de erro do banco) é anônimo: fica só em Development. Nos
// demais ambientes a resposta é apenas o status (Healthy, Degraded ou Unhealthy).
var healthCheckOptions = new HealthCheckOptions();

if (app.Environment.IsDevelopment())
{
    healthCheckOptions.ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse;
}

app.MapHealthChecks("health", healthCheckOptions);

app.UseRequestContextLogging();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();

app.UseCors(CorsExtensions.DefaultPolicyName);

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

await app.RunAsync();

// Torna a classe Program acessível aos testes de integração.
namespace TickestPristine.Web.Api
{
    public partial class Program;
}
