using System.ComponentModel.DataAnnotations;

namespace TickestPristine.Infrastructure.Database.Seeding;

/// <summary>
/// Configuração da seção "Admin": o usuário administrador criado pelo seed.
/// Só é lida (e validada) quando o seed roda, junto com as migrations.
/// </summary>
public sealed class AdminUserOptions
{
    public const string SectionName = "Admin";

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    [Required]
    public string FirstName { get; init; } = "Admin";

    [Required]
    public string LastName { get; init; } = "Master";
}
