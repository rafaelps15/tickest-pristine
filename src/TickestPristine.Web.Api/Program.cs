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

builder.Services.AddRateLimitingInternal(builder.Configuration);

builder.Services.AddCorsInternal(builder.Configuration);

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

WebApplication app = builder.Build();

app.MapEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerWithUi();

    app.ApplyMigrations();
}

await app.SeedAdminUserAsync();

app.MapHealthChecks("health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.UseRequestContextLogging();

app.UseSerilogRequestLogging();

app.UseExceptionHandler();

app.UseCors(CorsExtensions.DefaultPolicyName);

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

// REMARK: If you want to use Controllers, you'll need this.
app.MapControllers();

await app.RunAsync();

// REMARK: Required for functional and integration tests to work.
namespace TickestPristine.Web.Api
{
    public partial class Program;
}
