using TickestPristine.Infrastructure.Database;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using TickestPristine.Web.Api;

namespace TickestPristine.IntegrationTests;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@tickestpristine.dev";
    public const string AdminPassword = "Admin123!";

    /// <summary>
    /// Senha provisória com que o seed cria o administrador; o primeiro acesso troca para <see cref="AdminPassword"/>.
    /// </summary>
    private const string AdminInitialPassword = "ChangeMe123!";

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("tickestpristine")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly string _fileStorageRootPath = Path.Combine(Path.GetTempPath(), $"tickestpristine-tests-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());

        // Isola os arquivos de anexo enviados durante os testes.
        builder.UseSetting("FileStorage:RootPath", _fileStorageRootPath);

        // Configuração do JWT usada nos testes.
        builder.UseSetting("Jwt:Secret", "super-duper-secret-value-that-should-be-in-user-secrets");
        builder.UseSetting("Jwt:Issuer", "tickestpristine");
        builder.UseSetting("Jwt:Audience", "developers");
        builder.UseSetting("Jwt:ExpirationInMinutes", "60");

        // Eleva o limite de requisições para não bloquear os testes.
        builder.UseSetting("RateLimiting:Global:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Authentication:PermitLimit", "100000");

        // Credenciais do administrador inicial nos testes.
        builder.UseSetting("Admin:Email", AdminEmail);
        builder.UseSetting("Admin:FirstName", "Admin");
        builder.UseSetting("Admin:LastName", "Master");
        builder.UseSetting("Admin:Password", AdminInitialPassword);

        // CORS como em desenvolvimento: front local em qualquer porta.
        builder.UseSetting("Cors:AllowLoopbackOrigins", "true");

        // Os testes não usam os dados fictícios de desenvolvimento.
        builder.UseSetting("Seeding:SampleData", "false");
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        await CompleteAdministratorFirstAccessAsync();
    }

    /// <summary>
    /// O administrador do seed nasce com senha provisória: faz o primeiro acesso como o front faria. Falha aqui se o
    /// seed deixar de marcar a senha como provisória.
    /// </summary>
    private async Task CompleteAdministratorFirstAccessAsync()
    {
        using HttpClient client = CreateClient();

        var request = new { email = AdminEmail, currentPassword = AdminInitialPassword, newPassword = AdminPassword };

        HttpResponseMessage response = await client.PostAsJsonAsync("users/login/new-password", request);
        response.EnsureSuccessStatusCode();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();

        if (Directory.Exists(_fileStorageRootPath))
        {
            Directory.Delete(_fileStorageRootPath, recursive: true);
        }
    }
}
