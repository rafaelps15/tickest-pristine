using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.TicketAttachments.Delete;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.TicketAttachments;

public sealed class TicketAttachmentDeletedDomainEventHandlerTests : BaseHandlerTest
{
    private static readonly DateTime NowUtc = new(2026, 10, 27, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_Should_DeleteFileAndSetPurgedAt_WhenAttachmentIsDeleted()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        TicketAttachment attachment = await SeedDeletedAttachmentAsync(context);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        TicketAttachmentDeletedDomainEventHandler handler = CreateHandler(context, fileStorage);

        // Act
        await handler.Handle(new TicketAttachmentDeletedDomainEvent(attachment.Id, attachment.TicketId), CancellationToken.None);

        // Assert
        await fileStorage.Received(1).DeleteAsync("storage-key.pdf", Arg.Any<CancellationToken>());
        TicketAttachment stored = await context.TicketAttachments.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(a => a.Id == attachment.Id);
        stored.PurgedAtUtc.ShouldBe(NowUtc);
        stored.DeletedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Handle_Should_KeepPurgedAtNullAndPropagateFailure_WhenFileDeletionFails()
    {
        // Arrange: a falha sobe para o despacho dos eventos, que a registra no log
        await using TestDbContext context = CreateDbContext();
        TicketAttachment attachment = await SeedDeletedAttachmentAsync(context);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        fileStorage.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Disco indisponível")));
        TicketAttachmentDeletedDomainEventHandler handler = CreateHandler(context, fileStorage);

        // Act
        Exception? exception = await Record.ExceptionAsync(() =>
            handler.Handle(new TicketAttachmentDeletedDomainEvent(attachment.Id, attachment.TicketId), CancellationToken.None));

        // Assert
        exception.ShouldBeOfType<IOException>();
        TicketAttachment stored = await context.TicketAttachments.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(a => a.Id == attachment.Id);
        stored.DeletedAtUtc.ShouldNotBeNull();
        stored.PurgedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_Should_DoNothing_WhenFileWasAlreadyPurged()
    {
        // Arrange: o mesmo evento processado de novo
        await using TestDbContext context = CreateDbContext();
        TicketAttachment attachment = await SeedDeletedAttachmentAsync(context, purgedAtUtc: NowUtc.AddHours(-1));
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        TicketAttachmentDeletedDomainEventHandler handler = CreateHandler(context, fileStorage);

        // Act
        await handler.Handle(new TicketAttachmentDeletedDomainEvent(attachment.Id, attachment.TicketId), CancellationToken.None);

        // Assert
        await fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await context.TicketAttachments.IgnoreQueryFilters().SingleAsync(a => a.Id == attachment.Id)).PurgedAtUtc
            .ShouldBe(NowUtc.AddHours(-1));
    }

    private static TicketAttachmentDeletedDomainEventHandler CreateHandler(TestDbContext context, IFileStorage fileStorage)
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(NowUtc);

        return new TicketAttachmentDeletedDomainEventHandler(context, fileStorage, dateTimeProvider);
    }

    private static async Task<TicketAttachment> SeedDeletedAttachmentAsync(TestDbContext context, DateTime? purgedAtUtc = null)
    {
        var attachment = new TicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = Guid.NewGuid(),
            UploadedByUserId = Guid.NewGuid(),
            FileName = "report.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            StorageKey = "storage-key.pdf",
            UploadedAtUtc = NowUtc.AddDays(-1),
            DeletedAtUtc = NowUtc,
            PurgedAtUtc = purgedAtUtc
        };

        context.TicketAttachments.Add(attachment);
        await context.SaveChangesAsync();

        return attachment;
    }
}
