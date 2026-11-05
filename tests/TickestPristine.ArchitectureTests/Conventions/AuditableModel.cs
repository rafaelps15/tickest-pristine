using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Infrastructure.Audit;
using TickestPristine.Infrastructure.Database;
using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Tipos auditáveis lidos do modelo do EF da Infrastructure, montado sem conexão com o banco: o tipo, a tabela, as
/// propriedades que o interceptor de auditoria lê, as marcadas fora do log (<c>IsNotAudited</c>) e os <c>DbSet</c> do
/// <c>IApplicationDbContext</c> que gravam nele.
/// </summary>
internal static class AuditableModel
{
    private static readonly Lazy<IReadOnlyList<AuditableEntity>> LazyEntities = new(Load);

    public static IReadOnlyList<AuditableEntity> Entities => LazyEntities.Value;

    public static IReadOnlyList<string> Tables => [.. Entities.Select(entity => entity.Table)];

    public static IReadOnlyList<string> DbSets => [.. Entities.SelectMany(entity => entity.DbSets)];

    // Tipo fora do modelo do EF, como as fixtures dos testes, não tem nenhuma propriedade marcada.
    public static bool IsNotAudited(Type type, string property) =>
        Entities.FirstOrDefault(entity => entity.Type == type)?.NotAuditedProperties.Contains(property) == true;

    private static IReadOnlyList<AuditableEntity> Load()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=somente-modelo")
            .UseSnakeCaseNamingConvention()
            .Options;

        using var context = new ApplicationDbContext(options);

        return [.. context.Model.GetEntityTypes()
            .Where(entityType => typeof(IAuditable).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => new AuditableEntity(
                entityType.ClrType,
                entityType.GetTableName()!,
                [.. entityType.GetProperties().Select(property => property.Name)],
                [.. entityType.GetProperties().Where(property => !property.IsAudited()).Select(property => property.Name)],
                [.. typeof(IApplicationDbContext).GetProperties()
                    .Where(property => property.PropertyType == typeof(DbSet<>).MakeGenericType(entityType.ClrType))
                    .Select(property => property.Name)]))
            .OrderBy(entity => entity.Type.Name, StringComparer.Ordinal)];
    }
}

internal sealed record AuditableEntity(
    Type Type,
    string Table,
    IReadOnlyList<string> Properties,
    IReadOnlyList<string> NotAuditedProperties,
    IReadOnlyList<string> DbSets);
