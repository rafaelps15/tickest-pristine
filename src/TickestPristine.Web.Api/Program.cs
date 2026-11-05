using System.Reflection;
using TickestPristine.Application;
using TickestPristine.Infrastructure;
using Serilog;
using TickestPristine.Web.Api;
using TickestPristine.Web.Api.Extensions;
using TickestPristine.Web.Api.Middleware;

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

// Antes de tudo, para que toda resposta leve os cabeçalhos de segurança, inclusive as do Swagger e as de erro.
app.UseSecurityHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerWithUi();

    app.ApplyMigrations();
}

app.MapHealthCheckRoutes();

// Antes do UseExceptionHandler, para que o log da exceção também saia com o CorrelationId.
app.UseRequestContextLogging();

// Por fora do UseExceptionHandler, para registrar o status que o GlobalExceptionHandler devolveu (ex.: 409) em vez de
// ver a exceção passar e registrar 500.
app.UseSerilogRequestLogging();

app.UseExceptionHandler();

app.UseCors(CorsExtensions.DefaultPolicyName);

app.UseAuthentication();

// Entre a autenticação e a autorização: o limite separa por usuário autenticado e conta também as tentativas que a
// autorização recusaria (token inválido, rota sem permissão).
app.UseRateLimiter();

app.UseAuthorization();

await app.RunAsync();

// Torna a classe Program acessível aos testes de integração.
namespace TickestPristine.Web.Api
{
    public partial class Program;
}
