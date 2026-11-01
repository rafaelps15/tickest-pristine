using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.TicketAttachments;

public sealed class TicketAttachmentTests
{
    private static readonly Ticket Ticket = TicketTestData.Create();
    private static readonly Guid TicketId = Ticket.Id;
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
            Ticket,
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

    [Fact]
    public void RecordUpload_Should_RecordAttachmentAddedByUploaderWithFileName()
    {
        // Arrange
        TicketAttachment attachment = Create("report.pdf", "application/pdf", 1024).Value;

        // Act
        TicketHistory history = attachment.RecordUpload();

        // Assert
        history.TicketId.ShouldBe(TicketId);
        history.ChangedByUserId.ShouldBe(UploaderId);
        history.Action.ShouldBe(TicketHistoryAction.AttachmentAdded);
        history.NewValue.ShouldBe("report.pdf");
        history.OccurredAtUtc.ShouldBe(UploadedAtUtc);
    }

    [Fact]
    public void Delete_Should_MarkDeletedRaiseEventAndRecordRemovalByWhoDeleted()
    {
        // Arrange
        TicketAttachment attachment = Create("report.pdf", "application/pdf", 1024).Value;
        attachment.ClearDomainEvents();
        var managerId = Guid.NewGuid();
        DateTime deletedAtUtc = UploadedAtUtc.AddHours(1);

        // Act
        TicketHistory history = attachment.Delete(managerId, canDeleteAny: true, deletedAtUtc).Value;

        // Assert
        attachment.DeletedAtUtc.ShouldBe(deletedAtUtc);
        attachment.DomainEvents.ShouldHaveSingleItem().ShouldBe(new TicketAttachmentDeletedDomainEvent(attachment.Id, TicketId));
        history.TicketId.ShouldBe(TicketId);
        history.ChangedByUserId.ShouldBe(managerId);
        history.Action.ShouldBe(TicketHistoryAction.AttachmentRemoved);
        history.OccurredAtUtc.ShouldBe(deletedAtUtc);
    }

    [Fact]
    public void Delete_Should_AllowUploader_WhenUploaderCannotDeleteAny()
    {
        // Arrange
        TicketAttachment attachment = Create("report.pdf", "application/pdf", 1024).Value;

        // Act
        Result<TicketHistory> result = attachment.Delete(UploaderId, canDeleteAny: false, UploadedAtUtc.AddHours(1));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        attachment.DeletedAtUtc.ShouldBe(UploadedAtUtc.AddHours(1));
    }

    [Fact]
    public void Delete_Should_ReturnUnauthorizedAndChangeNothing_WhenCallerIsNotUploaderAndCannotDeleteAny()
    {
        // Arrange
        TicketAttachment attachment = Create("report.pdf", "application/pdf", 1024).Value;
        attachment.ClearDomainEvents();

        // Act
        Result<TicketHistory> result = attachment.Delete(Guid.NewGuid(), canDeleteAny: false, UploadedAtUtc.AddHours(1));

        // Assert
        result.Error.ShouldBe(UserErrors.Unauthorized());
        attachment.DeletedAtUtc.ShouldBeNull();
        attachment.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void MarkPurged_Should_RecordWhenFileWasRemovedFromStorage()
    {
        // Arrange
        TicketAttachment attachment = Create("report.pdf", "application/pdf", 1024).Value;

        // Act
        attachment.MarkPurged(UploadedAtUtc.AddDays(1));

        // Assert
        attachment.PurgedAtUtc.ShouldBe(UploadedAtUtc.AddDays(1));
    }

    private static Result<TicketAttachment> Create(string fileName, string contentType, long fileSizeBytes) =>
        TicketAttachment.Create(Ticket, UploaderId, fileName, contentType, fileSizeBytes, "chave", 0, 0, UploadedAtUtc);
}
