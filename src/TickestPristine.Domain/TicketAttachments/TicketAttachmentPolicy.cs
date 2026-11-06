namespace TickestPristine.Domain.TicketAttachments;

/// <summary>
/// Política de anexos dos chamados, a fonte única da regra: os limites e os tipos aceitos, com as extensões válidas de
/// cada um. A entidade confere o envio por ela, o inspetor de conteúdo recusa o tipo que ela não aceita, e a consulta da
/// política a devolve como está.
/// </summary>
public static class TicketAttachmentPolicy
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Anexos não excluídos que um chamado pode ter.
    /// </summary>
    public const int MaxAttachmentsPerTicket = 20;

    /// <summary>
    /// Soma dos tamanhos dos anexos não excluídos de um chamado.
    /// </summary>
    public const long MaxTotalBytesPerTicket = 100 * 1024 * 1024;

    /// <summary>
    /// Tipos aceitos e as extensões válidas de cada um, na ordem em que a política os apresenta. O nome do arquivo precisa
    /// ter uma delas: o tipo vem do cliente, e sem essa conferência um executável chegaria como "nota.pdf.exe" declarado
    /// como PDF.
    /// </summary>
    public static readonly IReadOnlyList<(string ContentType, string[] Extensions)> AllowedTypes =
    [
        ("image/png", [".png"]),
        ("image/jpeg", [".jpg", ".jpeg"]),
        ("image/gif", [".gif"]),
        ("image/webp", [".webp"]),
        ("application/pdf", [".pdf"]),
        ("application/msword", [".doc"]),
        ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [".docx"]),
        ("application/vnd.ms-excel", [".xls"]),
        ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [".xlsx"]),
        ("text/plain", [".txt"]),
        ("application/zip", [".zip"])
    ];

    /// <summary>
    /// Se a política aceita o tipo informado, sem considerar os parâmetros do tipo (como o charset).
    /// </summary>
    public static bool Accepts(string contentType) => ExtensionsOf(contentType) is not null;

    /// <summary>
    /// Extensões válidas do tipo informado, sem considerar os parâmetros do tipo (como o charset); nulo quando a política
    /// não aceita o tipo.
    /// </summary>
    public static string[]? ExtensionsOf(string contentType)
    {
        string mediaType = GetMediaType(contentType);

        return AllowedTypes
            .FirstOrDefault(type => string.Equals(type.ContentType, mediaType, StringComparison.OrdinalIgnoreCase))
            .Extensions;
    }

    /// <summary>
    /// Tipo sem os parâmetros: "text/plain; charset=utf-8" é o mesmo formato que "text/plain".
    /// </summary>
    public static string GetMediaType(string contentType) => contentType.Split(';', 2)[0].Trim();
}
