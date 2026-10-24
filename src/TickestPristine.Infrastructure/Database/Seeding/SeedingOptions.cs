namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Configuração da seção "Seeding".
/// </summary>
public sealed class SeedingOptions
{
    public const string SectionName = "Seeding";

    /// <summary>
    /// Cria a estrutura inicial de departamentos e setores num banco sem departamentos. Ligado por padrão.
    /// </summary>
    public bool DefaultOrganization { get; init; } = true;

    /// <summary>
    /// Cria chamados fictícios nos setores existentes, num banco sem chamados. Só para desenvolvimento.
    /// </summary>
    public bool SampleData { get; init; }

    /// <summary>
    /// Senha dos usuários fictícios que o <see cref="SampleData"/> cria (um grupo por role, menos Administrador). Fica
    /// nos user-secrets, como as outras senhas; é obrigatória quando o <see cref="SampleData"/> está ligado.
    /// </summary>
    public string? SampleUserPassword { get; init; }
}
