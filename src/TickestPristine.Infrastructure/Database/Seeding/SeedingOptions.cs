namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Configuração da seção "Seeding".
/// </summary>
public sealed class SeedingOptions
{
    public const string SectionName = "Seeding";

    /// <summary>
    /// Cria as roles iniciais (Atendente e Colaborador, a role padrão do cadastro) num banco sem outras roles além
    /// do Administrador. Ligado por padrão.
    /// </summary>
    public bool DefaultRoles { get; init; } = true;

    /// <summary>
    /// Cria a estrutura inicial de departamentos e setores num banco sem departamentos. Ligado por padrão.
    /// </summary>
    public bool DefaultOrganization { get; init; } = true;

    /// <summary>
    /// Cria chamados fictícios nos setores existentes, num banco sem chamados. Só para desenvolvimento.
    /// </summary>
    public bool SampleData { get; init; }
}
