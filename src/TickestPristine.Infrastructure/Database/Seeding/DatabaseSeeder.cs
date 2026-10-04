using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Executado pelo EF Core junto com as migrations (UseSeeding/UseAsyncSeeding), ou seja, a cada Migrate: cada seeder
/// confere antes o que já existe e não duplica nada. Roles e permissões padrão não passam por aqui: vêm das
/// migrations (HasData), porque o código depende dos ids fixos delas.
/// </summary>
internal sealed class DatabaseSeeder(
    AdminUserSeeder adminUserSeeder,
    DefaultOrganizationSeeder defaultOrganizationSeeder,
    SampleDataSeeder sampleDataSeeder)
{
    /// <summary>
    /// Versão síncrona usada por Migrate() e pelo "dotnet ef database update". Bloquear aqui é seguro: só roda na
    /// inicialização da aplicação ou na ferramenta de linha de comando, onde não há SynchronizationContext.
    /// </summary>
    public void Seed(DbContext context) => SeedAsync(context, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Roda os seeders em ordem: os chamados de exemplo dependem do administrador e dos setores já criados.
    /// </summary>
    public async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        var applicationDbContext = (ApplicationDbContext)context;

        await ReloadDatabaseTypesAsync(applicationDbContext, cancellationToken);

        await adminUserSeeder.SeedAsync(applicationDbContext, cancellationToken);
        await defaultOrganizationSeeder.SeedAsync(applicationDbContext, cancellationToken);
        await sampleDataSeeder.SeedAsync(applicationDbContext, cancellationToken);
    }

    /// <summary>
    /// As migrations podem criar extensões com tipos novos (ex.: citext) na mesma conexão; o Npgsql só passa a
    /// conhecê-los depois de recarregar os tipos do banco.
    /// </summary>
    private static async Task ReloadDatabaseTypesAsync(ApplicationDbContext context, CancellationToken cancellationToken)
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
