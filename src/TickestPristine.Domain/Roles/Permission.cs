using TickestPristine.SharedKernel;

namespace TickestPristine.Domain.Roles;

/// <summary>
/// Permissão do sistema, com o nome, a descrição e o grupo exibidos na tela. As permissões vêm do catálogo da
/// Application e vão para a tabela pelas migrations.
/// </summary>
public sealed class Permission : Entity
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Group { get; set; }

    /// <summary>
    /// Permissão administrativa: só pode ser concedida ou removida por quem já a possui.
    /// </summary>
    public bool IsAdministrative { get; set; }
}
