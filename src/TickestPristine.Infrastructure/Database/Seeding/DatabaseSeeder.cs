using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Executado pelo EF Core ao aplicar as migrations (UseSeeding/UseAsyncSeeding): cria o administrador inicial e,
/// com "Seeding:SampleData" ligado, os dados de exemplo. Roda a cada Migrate, por isso não duplica nada.
/// As roles padrão e suas permissões não passam por aqui: vêm das migrations (HasData).
/// </summary>
internal sealed class DatabaseSeeder(AdminUserSeeder adminUserSeeder, IConfiguration configuration)
{
    public void Seed(DbContext context)
    {
        adminUserSeeder.Seed(context);

        if (SampleDataEnabled)
        {
            SampleDataSeeder.Seed(context);
        }
    }

    public async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        await adminUserSeeder.SeedAsync(context, cancellationToken);

        if (SampleDataEnabled)
        {
            await SampleDataSeeder.SeedAsync(context, cancellationToken);
        }
    }

    private bool SampleDataEnabled =>
        bool.TryParse(configuration["Seeding:SampleData"], out bool enabled) && enabled;
}
