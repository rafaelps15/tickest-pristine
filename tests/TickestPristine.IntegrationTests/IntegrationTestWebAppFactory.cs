using TickestPristine.Infrastructure.Database;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using TickestPristine.Application.Abstractions.Notifications;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;
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
        builder.UseSetting("RateLimiting:AttachmentUpload:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:AttachmentDelete:PermitLimit", "100000");

        // Credenciais do administrador inicial nos testes.
        builder.UseSetting("Admin:Email", AdminEmail);
        builder.UseSetting("Admin:FirstName", "Admin");
        builder.UseSetting("Admin:LastName", "Master");
        builder.UseSetting("Admin:Password", AdminInitialPassword);

        // CORS como em desenvolvimento: front local em qualquer porta.
        builder.UseSetting("Cors:AllowLoopbackOrigins", "true");
        builder.UseSetting("Cors:AllowedOrigins:0", "https://app.example.com/");

        // Os testes não usam os dados fictícios de desenvolvimento.
        builder.UseSetting("Seeding:SampleData", "false");

        // O monitoramento do SLA alteraria cronômetros no meio dos outros testes da coleção: os testes chamam as voltas
        // dele diretamente. Os eventos de violação ficam guardados para as asserções.
        builder.UseSetting("SlaMonitor:Enabled", "false");

        // E-mails (spec 008): o envio real é trocado pelo CapturingEmailSender, mas as opções continuam validadas na
        // inicialização, como em produção.
        builder.UseSetting("Email:Host", "localhost");
        builder.UseSetting("Email:FromAddress", "no-reply@tickest.test");
        builder.UseSetting("Invitations:ActivationUrl", "https://tickest.test/ativar-conta");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<SlaBreachEventRecorder>();

            services.AddSingleton<CapturingEmailSender>();
            services.AddScoped<IEmailSender>(sp => sp.GetRequiredService<CapturingEmailSender>());
            services.AddScoped<IDomainEventHandler<TicketSlaBreachedDomainEvent>, RecordingSlaBreachedDomainEventHandler>();
        });
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
