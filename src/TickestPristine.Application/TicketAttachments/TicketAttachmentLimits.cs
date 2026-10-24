using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace TickestPristine.Application.TicketAttachments;

internal static class TicketAttachmentLimits
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Tipos aceitos e as extensões válidas de cada um. O nome do arquivo precisa ter uma delas: o tipo vem do cliente,
    /// e sem essa conferência um executável chegaria como "nota.pdf.exe" declarado como PDF.
    /// </summary>
    private static readonly FrozenDictionary<string, FrozenSet<string>> AllowedExtensionsByContentType =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = [".png"],
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/gif"] = [".gif"],
            ["image/webp"] = [".webp"],
            ["application/pdf"] = [".pdf"],
            ["application/msword"] = [".doc"],
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [".docx"],
            ["application/vnd.ms-excel"] = [".xls"],
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = [".xlsx"],
            ["text/plain"] = [".txt"],
            ["application/zip"] = [".zip"]
        }.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

    public static bool IsAllowedContentType(string? contentType) =>
        TryGetAllowedExtensions(contentType, out _);

    public static bool HasExtensionOfContentType(string? fileName, string? contentType) =>
        !string.IsNullOrEmpty(fileName) &&
        TryGetAllowedExtensions(contentType, out FrozenSet<string>? extensions) &&
        extensions.Contains(Path.GetExtension(fileName));

    /// <summary>
    /// Extensões aceitas para o tipo informado. Os parâmetros do tipo são ignorados: "text/plain; charset=utf-8" é o
    /// mesmo formato que "text/plain". Tipo vazio ou nulo não é aceito.
    /// </summary>
    private static bool TryGetAllowedExtensions(
        string? contentType,
        [NotNullWhen(true)] out FrozenSet<string>? extensions)
    {
        string mediaType = contentType?.Split(';', 2)[0].Trim() ?? string.Empty;

        if (mediaType.Length == 0)
        {
            extensions = null;
            return false;
        }

        return AllowedExtensionsByContentType.TryGetValue(mediaType, out extensions);
    }
}
