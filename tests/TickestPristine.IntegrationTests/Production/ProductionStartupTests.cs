using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace TickestPristine.IntegrationTests.Production;

public sealed partial class ProductionStartupTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private readonly IntegrationTestWebAppFactory _factory = factory;

    [Fact]
    public async Task Startup_Should_NotMigrateNorExposeSwagger_When_EnvironmentIsProduction()
    {
        // Arrange: um banco vazio no mesmo container e a API em Production apontando para ele
        string emptyDatabase = await CreateEmptyDatabaseAsync();
        await using WebApplicationFactory<Program> api = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Database", emptyDatabase);
        });
        using HttpClient client = api.CreateClient();

        // Act
        HttpResponseMessage swagger = await client.GetAsync("swagger/v1/swagger.json");
        HttpResponseMessage health = await client.GetAsync("health");

        // Assert: fora de Development não há Swagger, e a rota inexistente sem token cai na FallbackPolicy (401)
        swagger.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await health.Content.ReadAsStringAsync()).ShouldBe("Healthy");
        (await MigrationsHistoryExistsAsync(emptyDatabase)).ShouldBeFalse();
    }

    [Fact]
    public void ComposeFile_Should_UseThePostgresImageOfTheTests()
    {
        // Arrange
        string compose = File.ReadAllText(Path.Combine(RepositoryRoot(), "docker-compose.yml"));

        // Act
        Match image = ComposePostgresImageRegex().Match(compose);

        // Assert
        image.Success.ShouldBeTrue("Serviço postgres sem image no docker-compose.yml.");
        image.Groups["image"].Value.ShouldBe(IntegrationTestWebAppFactory.PostgresImage);
    }

    private static async Task<bool> MigrationsHistoryExistsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL";

        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TickestPristine.slnx")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull();

        return directory.FullName;
    }

    [GeneratedRegex(@"^\s*postgres:\s*\r?\n\s*image:\s*(?<image>\S+)", RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ComposePostgresImageRegex();
}
