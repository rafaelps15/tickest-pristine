using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace TickestPristine.IntegrationTests.Middleware;

public sealed class HealthChecksTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private readonly IntegrationTestWebAppFactory _factory = factory;

    [Fact]
    public async Task HealthRoutes_Should_ReturnOk_When_DependenciesAreUp()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage live = await HttpClient.GetAsync("health/live");
        HttpResponseMessage ready = await HttpClient.GetAsync("health/ready");
        HttpResponseMessage health = await HttpClient.GetAsync("health");

        // Assert
        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthRoutes_Should_SeparateLivenessFromReadinessWithStatusOnly_When_DatabaseDoesNotExist()
    {
        // Arrange: a API em Production, sem migração na subida, apontando para um banco que não existe no container
        string missingDatabase = new NpgsqlConnectionStringBuilder(DatabaseConnectionString())
        {
            Database = $"inexistente_{Guid.NewGuid():N}"
        }.ConnectionString;
        await using WebApplicationFactory<Program> api = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Database", missingDatabase);
        });
        using HttpClient client = api.CreateClient();

        // Act
        HttpResponseMessage live = await client.GetAsync("health/live");
        HttpResponseMessage ready = await client.GetAsync("health/ready");

        // Assert: fora de Development, o corpo traz só o status
        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await live.Content.ReadAsStringAsync()).ShouldBe("Healthy");
        ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await ready.Content.ReadAsStringAsync()).ShouldBe("Unhealthy");
    }

    [Fact]
    public async Task HealthReady_Should_ReturnServiceUnavailable_When_FileStorageIsNotWritable()
    {
        // Arrange: a pasta dos anexos fica "dentro" de um arquivo comum, então não pode ser criada nem receber escrita
        string blockingFile = Path.GetTempFileName();

        try
        {
            await using WebApplicationFactory<Program> api = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting("FileStorage:RootPath", Path.Combine(blockingFile, "attachments"));
            });
            using HttpClient client = api.CreateClient();

            // Act
            HttpResponseMessage ready = await client.GetAsync("health/ready");
            HttpResponseMessage live = await client.GetAsync("health/live");

            // Assert
            ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            live.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        finally
        {
            File.Delete(blockingFile);
        }
    }

    [Fact]
    public async Task HealthReady_Should_ReturnServiceUnavailable_When_ConfiguredCacheIsUnreachable()
    {
        // Arrange: o Redis configurado numa porta onde nada escuta, com tempo de conexão curto
        await using WebApplicationFactory<Program> api = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Cache", "127.0.0.1:1,connectTimeout=500,abortConnect=true");
        });
        using HttpClient client = api.CreateClient();

        // Act
        HttpResponseMessage ready = await client.GetAsync("health/ready");
        HttpResponseMessage live = await client.GetAsync("health/live");

        // Assert
        ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        live.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private string DatabaseConnectionString() =>
        Services.GetRequiredService<IConfiguration>().GetConnectionString("Database")!;
}
