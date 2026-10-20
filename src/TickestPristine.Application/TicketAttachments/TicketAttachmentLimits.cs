using System.Collections.Frozen;

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

    public static bool IsAllowedContentType(string contentType) =>
        AllowedExtensionsByContentType.ContainsKey(contentType);

    public static bool HasExtensionOfContentType(string fileName, string contentType) =>
        AllowedExtensionsByContentType.TryGetValue(contentType, out FrozenSet<string>? extensions) &&
        extensions.Contains(Path.GetExtension(fileName));
}
