namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Configuração da seção "Seeding".
/// </summary>
public sealed class SeedingOptions
{
    public const string SectionName = "Seeding";

    /// <summary>
    /// Cria departamentos, setores e chamados fictícios num banco vazio. Só para desenvolvimento.
    /// </summary>
    public bool SampleData { get; init; }
}
