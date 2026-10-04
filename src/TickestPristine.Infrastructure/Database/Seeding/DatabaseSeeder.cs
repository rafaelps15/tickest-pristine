using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Executado pelo EF Core ao aplicar as migrations (UseSeeding/UseAsyncSeeding): cria o administrador inicial,
/// a estrutura inicial de departamentos e setores (com "Seeding:DefaultOrganization", ligado por padrão) e, com
/// "Seeding:SampleData" ligado, os chamados de exemplo. Roda a cada Migrate, por isso não duplica nada.
/// As roles padrão e suas permissões não passam por aqui: vêm das migrations (HasData).
/// </summary>
internal sealed class DatabaseSeeder(AdminUserSeeder adminUserSeeder, IOptions<SeedingOptions> seedingOptions)
{
    public void Seed(DbContext context)
    {
        ReloadDatabaseTypes(context);

        adminUserSeeder.Seed(context);

        if (DefaultOrganizationEnabled)
        {
            DefaultOrganizationSeeder.Seed(context);
        }

        if (SampleDataEnabled)
        {
            SampleDataSeeder.Seed(context);
        }
    }

    public async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        await ReloadDatabaseTypesAsync(context, cancellationToken);

        await adminUserSeeder.SeedAsync(context, cancellationToken);

        if (DefaultOrganizationEnabled)
        {
            await DefaultOrganizationSeeder.SeedAsync(context, cancellationToken);
        }

        if (SampleDataEnabled)
        {
            await SampleDataSeeder.SeedAsync(context, cancellationToken);
        }
    }

    private bool DefaultOrganizationEnabled => seedingOptions.Value.DefaultOrganization;

    private bool SampleDataEnabled => seedingOptions.Value.SampleData;

    /// <summary>
    /// As migrations podem criar extensões com tipos novos (ex.: citext) na mesma conexão; o Npgsql só passa a
    /// conhecê-los depois de recarregar os tipos do banco.
    /// </summary>
    private static void ReloadDatabaseTypes(DbContext context)
    {
        context.Database.OpenConnection();

        try
        {
            ((NpgsqlConnection)context.Database.GetDbConnection()).ReloadTypes();
        }
        finally
        {
            context.Database.CloseConnection();
        }
    }

    private static async Task ReloadDatabaseTypesAsync(DbContext context, CancellationToken cancellationToken)
    {
        await context.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await ((NpgsqlConnection)context.Database.GetDbConnection()).ReloadTypesAsync(cancellationToken);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
