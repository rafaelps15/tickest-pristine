using TickestPristine.Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using TickestPristine.Web.Api;

namespace TickestPristine.IntegrationTests;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("tickestpristine")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly string _fileStorageRootPath = Path.Combine(Path.GetTempPath(), $"tickestpristine-tests-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());

        // Isolate uploaded attachment files from the developer's real App_Data folder.
        builder.UseSetting("FileStorage:RootPath", _fileStorageRootPath);

        // Provide deterministic JWT settings so tokens can be issued and validated in tests.
        builder.UseSetting("Jwt:Secret", "super-duper-secret-value-that-should-be-in-user-secrets");
        builder.UseSetting("Jwt:Issuer", "tickestpristine");
        builder.UseSetting("Jwt:Audience", "developers");
        builder.UseSetting("Jwt:ExpirationInMinutes", "60");

        // Relax rate limiting so the test suite is not throttled.
        builder.UseSetting("RateLimiting:Global:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Authentication:PermitLimit", "100000");

        // Deterministic admin bootstrap credentials for the seeded AdminMaster user.
        builder.UseSetting("Admin:Email", "admin@tickestpristine.dev");
        builder.UseSetting("Admin:FirstName", "Admin");
        builder.UseSetting("Admin:LastName", "Master");
        builder.UseSetting("Admin:Password", "ChangeMe123!");
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();
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
