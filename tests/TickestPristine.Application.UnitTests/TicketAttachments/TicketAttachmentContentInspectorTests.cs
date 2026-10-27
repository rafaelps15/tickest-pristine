using System.IO.Compression;
using System.Text;
using TickestPristine.Application.TicketAttachments;

namespace TickestPristine.Application.UnitTests.TicketAttachments;

public sealed class TicketAttachmentContentInspectorTests
{
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Começo de um executável do Windows ("MZ"): o conteúdo inválido usado para os tipos com assinatura.
    /// </summary>
    private static readonly byte[] Executable = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    public static TheoryData<string, byte[]> ValidSamples => new()
    {
        { "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00] },
        { "image/jpeg", [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10] },
        { "image/gif", Encoding.ASCII.GetBytes("GIF89a....") },
        { "image/webp", Encoding.ASCII.GetBytes("RIFF\u0010\0\0\0WEBPVP8 ") },
        { "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7\n%conteúdo") },
        { "application/msword", [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00] },
        { "application/vnd.ms-excel", [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00] },
        { Docx, Package("word/document.xml") },
        { Xlsx, Package("xl/workbook.xml") },
        { "text/plain", Encoding.UTF8.GetBytes("Anotações do chamado.\nSegunda linha.") },
        { "application/zip", Package("dados.csv") }
    };

    public static TheoryData<string, byte[]> InvalidSamples => new()
    {
        { "image/png", Executable },
        { "image/jpeg", Executable },
        { "image/gif", Encoding.ASCII.GetBytes("GIF90a....") },
        { "image/webp", Encoding.ASCII.GetBytes("RIFF\u0010\0\0\0WAVEfmt ") },
        { "application/pdf", Encoding.ASCII.GetBytes("<html>não é PDF</html>") },
        { "application/msword", Package("word/document.xml") },
        { "application/vnd.ms-excel", Executable },
        // Pacote do Excel declarado como Word, e ZIP comum declarado como planilha.
        { Docx, Package("xl/workbook.xml") },
        { Xlsx, Package("dados.csv") },
        { "text/plain", [0x41, 0x42, 0x00, 0x43] },
        { "application/zip", Executable }
    };

    [Theory]
    [MemberData(nameof(ValidSamples))]
    public async Task MatchesAsync_Should_ReturnTrue_WhenContentMatchesContentType(string contentType, byte[] content)
    {
        // Arrange
        using var stream = new MemoryStream(content);

        // Act
        bool matches = await TicketAttachmentContentInspector.MatchesAsync(stream, contentType, CancellationToken.None);

        // Assert
        matches.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(InvalidSamples))]
    public async Task MatchesAsync_Should_ReturnFalse_WhenContentDoesNotMatchContentType(string contentType, byte[] content)
    {
        // Arrange
        using var stream = new MemoryStream(content);

        // Act
        bool matches = await TicketAttachmentContentInspector.MatchesAsync(stream, contentType, CancellationToken.None);

        // Assert
        matches.ShouldBeFalse();
    }

    [Fact]
    public async Task MatchesAsync_Should_ReturnFalse_WhenOfficePackageIsCorrupted()
    {
        // Arrange: assinatura de ZIP seguida de lixo
        using var stream = new MemoryStream([0x50, 0x4B, 0x03, 0x04, 0x01, 0x02, 0x03]);

        // Act
        bool matches = await TicketAttachmentContentInspector.MatchesAsync(stream, Xlsx, CancellationToken.None);

        // Assert
        matches.ShouldBeFalse();
    }

    [Fact]
    public async Task MatchesAsync_Should_IgnoreParametersAndCase_WhenContentTypeHasThem()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("texto"));

        // Act
        bool matches = await TicketAttachmentContentInspector.MatchesAsync(
            stream,
            "Text/Plain; charset=utf-8",
            CancellationToken.None);

        // Assert
        matches.ShouldBeTrue();
    }

    [Fact]
    public async Task MatchesAsync_Should_ReturnFalse_WhenTextIsEmpty()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        bool matches = await TicketAttachmentContentInspector.MatchesAsync(stream, "text/plain", CancellationToken.None);

        // Assert
        matches.ShouldBeFalse();
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData(Xlsx)]
    public async Task MatchesAsync_Should_RestoreStreamPosition_WhenInspectionEnds(string contentType)
    {
        // Arrange: o arquivo é gravado depois da conferência, a partir da mesma posição
        byte[] content = contentType == Xlsx ? Package("xl/workbook.xml") : Encoding.UTF8.GetBytes(new string('a', 20_000));
        using var stream = new MemoryStream(content);

        // Act
        await TicketAttachmentContentInspector.MatchesAsync(stream, contentType, CancellationToken.None);

        // Assert
        stream.Position.ShouldBe(0);
    }

    /// <summary>
    /// Pacote ZIP com uma entrada no caminho informado.
    /// </summary>
    private static byte[] Package(string entryName)
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName);
            using StreamWriter writer = new(entry.Open());
            writer.Write("<conteudo/>");
        }

        return buffer.ToArray();
    }
}
