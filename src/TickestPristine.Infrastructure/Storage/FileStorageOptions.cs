using System.ComponentModel.DataAnnotations;

namespace TickestPristine.Infrastructure.Storage;

/// <summary>
/// Configuração da seção "FileStorage": pasta onde ficam os arquivos dos anexos.
/// </summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    [Required]
    public string RootPath { get; init; } = Path.Combine(AppContext.BaseDirectory, "App_Data", "attachments");
}
