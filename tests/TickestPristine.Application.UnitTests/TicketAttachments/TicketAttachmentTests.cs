using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.Domain.TicketAttachments;
using TickestPristine.Domain.TicketHistories;

namespace TickestPristine.Application.UnitTests.TicketAttachments;

public sealed class TicketAttachmentTests
{
    private static readonly Ticket Ticket = TicketTestData.Create();
    private static readonly Guid TicketId = Ticket.Id;
    private static readonly Guid UploaderId = Guid.NewGuid();
    private static readonly DateTime UploadedAtUtc = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AddAttachment_Should_CreateAttachmentAndHistoryAndRaiseEvent_WhenFileIsAccepted()
    {
        // Act
        Result<TicketAttachmentUpload> result = AddAttachment("report.pdf", "application/pdf", 1024);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        TicketAttachment attachment = result.Value.Attachment;
        attachment.Id.ShouldNotBe(Guid.Empty);
        attachment.TicketId.ShouldBe(TicketId);
        attachment.UploadedByUserId.ShouldBe(UploaderId);
        attachment.FileName.ShouldBe("report.pdf");
        attachment.ContentType.ShouldBe("application/pdf");
        attachment.FileSizeBytes.ShouldBe(1024);
        attachment.StorageKey.ShouldBe("chave");
        attachment.UploadedAtUtc.ShouldBe(UploadedAtUtc);
        attachment.DomainEvents.ShouldHaveSingleItem().ShouldBe(
            new TicketAttachmentUploadedDomainEvent(attachment.Id, TicketId, UploaderId, "report.pdf"));

        TicketHistoryEntry history = result.Value.History;
        history.TicketId.ShouldBe(TicketId);
        history.ChangedByUserId.ShouldBe(UploaderId);
        history.Action.ShouldBe(TicketHistoryAction.AttachmentAdded);
        history.NewValue.ShouldBe("report.pdf");
        history.OccurredAtUtc.ShouldBe(UploadedAtUtc);
    }

    [Fact]
    public void AddAttachment_Should_NotChangeTicket_WhenAttachmentIsAdded()
    {
        // Arrange
        Ticket ticket = TicketTestData.Create();
        DateTime lastActivityAtUtc = ticket.LastActivityAtUtc;
        TicketWaitingOn? waitingOn = ticket.WaitingOn;
        ticket.ClearDomainEvents();

        // Act
        ticket.AddAttachment(UploaderId, "report.pdf", "application/pdf", 1024, "chave", true, 0, 0, UploadedAtUtc);

        // Assert: a última atividade sai da projeção, depois do commit.
        ticket.LastActivityAtUtc.ShouldBe(lastActivityAtUtc);
        ticket.WaitingOn.ShouldBe(waitingOn);
        ticket.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void AddAttachment_Should_ReturnContentMismatch_WhenContentDoesNotMatchContentType()
    {
        // Act
        Result<TicketAttachmentUpload> result = AddAttachment("report.pdf", "application/pdf", 1024, contentMatches: false);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.ContentMismatch);
    }

    [Fact]
    public void AddAttachment_Should_ReturnUnsupportedContentType_WhenContentTypeIsNotAllowed()
    {
        // Act
        Result<TicketAttachmentUpload> result = AddAttachment("setup.exe", "application/x-msdownload", 1024);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.UnsupportedContentType("application/x-msdownload"));
    }

    [Fact]
    public void AddAttachment_Should_ReturnExtensionMismatch_WhenFileNameHasNoExtensionOfContentType()
    {
        // Act: executável declarado como PDF
        Result<TicketAttachmentUpload> result = AddAttachment("nota.pdf.exe", "application/pdf", 1024);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.ExtensionMismatch("application/pdf"));
    }

    [Theory]
    [InlineData("Foto.JPEG", "image/jpeg")]
    [InlineData("notas.txt", "text/plain; charset=utf-8")]
    [InlineData("Planilha.XLSX", "APPLICATION/VND.OPENXMLFORMATS-OFFICEDOCUMENT.SPREADSHEETML.SHEET")]
    public void AddAttachment_Should_AcceptFile_WhenExtensionOrTypeDifferOnlyInCaseOrTypeHasParameters(
        string fileName,
        string contentType)
    {
        // Act
        Result<TicketAttachmentUpload> result = AddAttachment(fileName, contentType, 1024);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(10 * 1024 * 1024, true)]
    [InlineData(10 * 1024 * 1024 + 1, false)]
    public void AddAttachment_Should_AcceptFilesUpToMaxSize_WhenSizeIsGiven(long fileSizeBytes, bool expectedSuccess)
    {
        // Act
        Result<TicketAttachmentUpload> result = AddAttachment("report.pdf", "application/pdf", fileSizeBytes);

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
    public void AddAttachment_Should_RespectTicketLimits_WhenTicketAlreadyHasAttachments(
        int currentCount,
        long currentTotalBytes,
        bool expectedSuccess)
    {
        // Act: o anexo novo tem 1.024 bytes
        Result<TicketAttachmentUpload> result = Ticket.AddAttachment(
            UploaderId,
            "report.pdf",
            "application/pdf",
            1024,
            "chave",
            contentMatches: true,
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
    public void AddAttachment_Should_CheckContentThenFileThenTicketLimits_WhenSeveralRulesFail()
    {
        // Arrange: extensão errada, arquivo grande demais e chamado no limite ao mesmo tempo
        static Result<TicketAttachmentUpload> Add(bool contentMatches, string fileName, long fileSizeBytes) =>
            Ticket.AddAttachment(
                UploaderId,
                fileName,
                "application/pdf",
                fileSizeBytes,
                "chave",
                contentMatches,
                TicketAttachmentPolicy.MaxAttachmentsPerTicket,
                0,
                UploadedAtUtc);

        // Act
        Result<TicketAttachmentUpload> contentFirst = Add(contentMatches: false, "nota.exe", 11 * 1024 * 1024);
        Result<TicketAttachmentUpload> extensionNext = Add(contentMatches: true, "nota.exe", 11 * 1024 * 1024);
        Result<TicketAttachmentUpload> sizeNext = Add(contentMatches: true, "nota.pdf", 11 * 1024 * 1024);
        Result<TicketAttachmentUpload> limitLast = Add(contentMatches: true, "nota.pdf", 1024);

        // Assert
        contentFirst.Error.ShouldBe(TicketAttachmentErrors.ContentMismatch);
        extensionNext.Error.ShouldBe(TicketAttachmentErrors.ExtensionMismatch("application/pdf"));
        sizeNext.Error.ShouldBe(TicketAttachmentErrors.FileTooLarge(10));
        limitLast.Error.ShouldBe(TicketAttachmentErrors.TicketLimitReached(20, 100));
    }

    [Fact]
    public void Delete_Should_MarkDeletedRaiseEventAndRecordRemovalByWhoDeleted()
    {
        // Arrange
        TicketAttachment attachment = CreateAttachment();
        var managerId = Guid.NewGuid();
        DateTime deletedAtUtc = UploadedAtUtc.AddHours(1);

        // Act
        TicketHistoryEntry history = attachment.Delete(managerId, canDeleteAny: true, deletedAtUtc).Value;

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
        TicketAttachment attachment = CreateAttachment();

        // Act
        Result<TicketHistoryEntry> result = attachment.Delete(UploaderId, canDeleteAny: false, UploadedAtUtc.AddHours(1));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        attachment.DeletedAtUtc.ShouldBe(UploadedAtUtc.AddHours(1));
    }

    [Fact]
    public void Delete_Should_ReturnUnauthorizedAndChangeNothing_WhenCallerIsNotUploaderAndCannotDeleteAny()
    {
        // Arrange
        TicketAttachment attachment = CreateAttachment();

        // Act
        Result<TicketHistoryEntry> result = attachment.Delete(Guid.NewGuid(), canDeleteAny: false, UploadedAtUtc.AddHours(1));

        // Assert
        result.Error.ShouldBe(UserErrors.Unauthorized());
        attachment.DeletedAtUtc.ShouldBeNull();
        attachment.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void RecordFilePurge_Should_RecordPurgeAndReturnTrue_WhenAttachmentIsDeletedAndNotPurged()
    {
        // Arrange
        TicketAttachment attachment = CreateAttachment();
        attachment.Delete(UploaderId, canDeleteAny: false, UploadedAtUtc.AddHours(1));

        // Act
        bool recorded = attachment.RecordFilePurge(UploadedAtUtc.AddDays(1));

        // Assert
        recorded.ShouldBeTrue();
        attachment.PurgedAtUtc.ShouldBe(UploadedAtUtc.AddDays(1));
    }

    [Fact]
    public void RecordFilePurge_Should_ReturnFalseAndChangeNothing_WhenAttachmentIsNotDeleted()
    {
        // Arrange
        TicketAttachment attachment = CreateAttachment();

        // Act
        bool recorded = attachment.RecordFilePurge(UploadedAtUtc.AddDays(1));

        // Assert
        recorded.ShouldBeFalse();
        attachment.PurgedAtUtc.ShouldBeNull();
    }

    [Fact]
    public void RecordFilePurge_Should_ReturnFalseAndKeepFirstPurge_WhenPurgeWasAlreadyRecorded()
    {
        // Arrange
        TicketAttachment attachment = CreateAttachment();
        attachment.Delete(UploaderId, canDeleteAny: false, UploadedAtUtc.AddHours(1));
        attachment.RecordFilePurge(UploadedAtUtc.AddDays(1));

        // Act
        bool recorded = attachment.RecordFilePurge(UploadedAtUtc.AddDays(2));

        // Assert
        recorded.ShouldBeFalse();
        attachment.PurgedAtUtc.ShouldBe(UploadedAtUtc.AddDays(1));
    }

    private static Result<TicketAttachmentUpload> AddAttachment(
        string fileName,
        string contentType,
        long fileSizeBytes,
        bool contentMatches = true) =>
        Ticket.AddAttachment(UploaderId, fileName, contentType, fileSizeBytes, "chave", contentMatches, 0, 0, UploadedAtUtc);

    private static TicketAttachment CreateAttachment()
    {
        TicketAttachment attachment = AddAttachment("report.pdf", "application/pdf", 1024).Value.Attachment;
        attachment.ClearDomainEvents();

        return attachment;
    }
}
