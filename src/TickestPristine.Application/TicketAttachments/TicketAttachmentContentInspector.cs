using System.IO.Compression;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.TicketAttachments;

/// <summary>
/// Confere se o conteúdo do anexo corresponde ao tipo declarado pelo cliente, pelos primeiros bytes do arquivo. DOCX e
/// XLSX são pacotes ZIP: além da assinatura, o índice do pacote precisa ter a pasta do Word (<c>word/</c>) ou do Excel
/// (<c>xl/</c>). TXT não tem assinatura: basta não haver byte nulo no começo do arquivo, o que recusa binários.
/// </summary>
internal static class TicketAttachmentContentInspector
{
    /// <summary>
    /// Trecho inicial lido do arquivo: cobre todas as assinaturas e a amostra do texto.
    /// </summary>
    private const int SampleSize = 8 * 1024;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Gif87Signature = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89Signature = "GIF89a"u8.ToArray();
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebpMarker = "WEBP"u8.ToArray();
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    /// <summary>
    /// Formato OLE2 dos arquivos antigos do Office (DOC e XLS).
    /// </summary>
    private static readonly byte[] Ole2Signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    /// <summary>
    /// ZIP vazio: só o registro de fim do índice.
    /// </summary>
    private static readonly byte[] EmptyZipSignature = [0x50, 0x4B, 0x05, 0x06];

    /// <summary>
    /// Indica se o conteúdo corresponde ao tipo informado. Lê o começo do arquivo (e, para DOCX e XLSX, o índice do
    /// pacote) e devolve o fluxo à posição em que estava. Exige um fluxo posicionável.
    /// </summary>
    public static async Task<bool> MatchesAsync(Stream content, string contentType, CancellationToken cancellationToken)
    {
        long start = content.Position;

        try
        {
            byte[] sample = new byte[SampleSize];
            int read = await content.ReadAtLeastAsync(sample, SampleSize, throwOnEndOfStream: false, cancellationToken);
            ReadOnlyMemory<byte> header = sample.AsMemory(0, read);

            string mediaType = TicketAttachment.GetMediaType(contentType);

            if (Is(mediaType, "application/vnd.openxmlformats-officedocument.wordprocessingml.document"))
            {
                return StartsWith(header, ZipSignature) && await HasPackageFolderAsync(content, start, "word/", cancellationToken);
            }

            if (Is(mediaType, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"))
            {
                return StartsWith(header, ZipSignature) && await HasPackageFolderAsync(content, start, "xl/", cancellationToken);
            }

            return MatchesSignature(mediaType, header.Span);
        }
        finally
        {
            content.Position = start;
        }
    }

    private static bool MatchesSignature(string mediaType, ReadOnlySpan<byte> header)
    {
        if (Is(mediaType, "image/png"))
        {
            return header.StartsWith(PngSignature);
        }

        if (Is(mediaType, "image/jpeg"))
        {
            return header.StartsWith(JpegSignature);
        }

        if (Is(mediaType, "image/gif"))
        {
            return header.StartsWith(Gif87Signature) || header.StartsWith(Gif89Signature);
        }

        if (Is(mediaType, "image/webp"))
        {
            // "RIFF", 4 bytes de tamanho e "WEBP".
            return header.StartsWith(RiffSignature) && header.Length >= 12 && header.Slice(8, 4).SequenceEqual(WebpMarker);
        }

        if (Is(mediaType, "application/pdf"))
        {
            return header.StartsWith(PdfSignature);
        }

        if (Is(mediaType, "application/msword") || Is(mediaType, "application/vnd.ms-excel"))
        {
            return header.StartsWith(Ole2Signature);
        }

        if (Is(mediaType, "application/zip"))
        {
            return header.StartsWith(ZipSignature) || header.StartsWith(EmptyZipSignature);
        }

        if (Is(mediaType, "text/plain"))
        {
            return !header.IsEmpty && !header.Contains((byte)0);
        }

        return false;
    }

    /// <summary>
    /// Indica se o pacote ZIP tem alguma entrada dentro da pasta informada. Só o índice é lido: nada é descompactado.
    /// Pacote corrompido não corresponde a nenhum tipo.
    /// </summary>
    private static async Task<bool> HasPackageFolderAsync(
        Stream content,
        long start,
        string folder,
        CancellationToken cancellationToken)
    {
        content.Position = start;

        try
        {
            await using ZipArchive archive = await ZipArchive.CreateAsync(
                content,
                ZipArchiveMode.Read,
                leaveOpen: true,
                entryNameEncoding: null,
                cancellationToken);

            return archive.Entries.Any(entry => entry.FullName.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool StartsWith(ReadOnlyMemory<byte> header, byte[] signature) => header.Span.StartsWith(signature);

    private static bool Is(string mediaType, string expected) =>
        string.Equals(mediaType, expected, StringComparison.OrdinalIgnoreCase);
}
