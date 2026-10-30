using TickestPristine.Domain.Tickets;

namespace TickestPristine.Application.UnitTests.TicketAttachments;

public sealed class TicketAttachmentTests
{
    private static readonly Guid TicketId = Guid.NewGuid();
    private static readonly Guid UploaderId = Guid.NewGuid();
    private static readonly DateTime UploadedAtUtc = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_Should_CreateAttachmentAndRaiseEvent_WhenFileIsAccepted()
    {
        // Act
        Result<TicketAttachment> result = Create("report.pdf", "application/pdf", 1024);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        TicketAttachment attachment = result.Value;
        attachment.Id.ShouldNotBe(Guid.Empty);
        attachment.TicketId.ShouldBe(TicketId);
        attachment.UploadedByUserId.ShouldBe(UploaderId);
        attachment.FileName.ShouldBe("report.pdf");
        attachment.ContentType.ShouldBe("application/pdf");
        attachment.FileSizeBytes.ShouldBe(1024);
        attachment.StorageKey.ShouldBe("chave");
        attachment.UploadedAtUtc.ShouldBe(UploadedAtUtc);
        attachment.DomainEvents.OfType<TicketAttachmentUploadedDomainEvent>().ShouldContain(e =>
            e.AttachmentId == attachment.Id &&
            e.TicketId == TicketId &&
            e.UploadedByUserId == UploaderId &&
            e.FileName == "report.pdf");
    }

    [Fact]
    public void Create_Should_ReturnUnsupportedContentType_WhenContentTypeIsNotAllowed()
    {
        // Act
        Result<TicketAttachment> result = Create("setup.exe", "application/x-msdownload", 1024);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.UnsupportedContentType("application/x-msdownload"));
    }

    [Fact]
    public void Create_Should_ReturnExtensionMismatch_WhenFileNameHasNoExtensionOfContentType()
    {
        // Act: executável declarado como PDF
        Result<TicketAttachment> result = Create("nota.pdf.exe", "application/pdf", 1024);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.ExtensionMismatch("application/pdf"));
    }

    [Theory]
    [InlineData("Foto.JPEG", "image/jpeg")]
    [InlineData("notas.txt", "text/plain; charset=utf-8")]
    [InlineData("Planilha.XLSX", "APPLICATION/VND.OPENXMLFORMATS-OFFICEDOCUMENT.SPREADSHEETML.SHEET")]
    public void Create_Should_AcceptFile_WhenExtensionOrTypeDifferOnlyInCaseOrTypeHasParameters(string fileName, string contentType)
    {
        // Act
        Result<TicketAttachment> result = Create(fileName, contentType, 1024);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(10 * 1024 * 1024, true)]
    [InlineData(10 * 1024 * 1024 + 1, false)]
    public void Create_Should_AcceptFilesUpToMaxSize_WhenSizeIsGiven(long fileSizeBytes, bool expectedSuccess)
    {
        // Act
        Result<TicketAttachment> result = Create("report.pdf", "application/pdf", fileSizeBytes);

        // Assert
        result.IsSuccess.ShouldBe(expectedSuccess);

        if (!expectedSuccess)
        {
            result.Error.ShouldBe(TicketAttachmentErrors.FileTooLarge(10));
        }
    }

    [Theory]
    [InlineData(19, 0, true)]
    [InlineData(20, 0, false)]
    [InlineData(1, 100 * 1024 * 1024 - 1024, true)]
    [InlineData(1, 100 * 1024 * 1024 - 1023, false)]
    public void Create_Should_RespectTicketLimits_WhenTicketAlreadyHasAttachments(
        int currentCount,
        long currentTotalBytes,
        bool expectedSuccess)
    {
        // Act: o anexo novo tem 1.024 bytes
        Result<TicketAttachment> result = TicketAttachment.Create(
            TicketId,
            UploaderId,
            "report.pdf",
            "application/pdf",
            1024,
            "chave",
            currentCount,
            currentTotalBytes,
            UploadedAtUtc);

        // Assert
        result.IsSuccess.ShouldBe(expectedSuccess);

        if (!expectedSuccess)
        {
            result.Error.ShouldBe(TicketAttachmentErrors.TicketLimitReached(20, 100));
        }
    }

    private static Result<TicketAttachment> Create(string fileName, string contentType, long fileSizeBytes) =>
        TicketAttachment.Create(TicketId, UploaderId, fileName, contentType, fileSizeBytes, "chave", 0, 0, UploadedAtUtc);
}
