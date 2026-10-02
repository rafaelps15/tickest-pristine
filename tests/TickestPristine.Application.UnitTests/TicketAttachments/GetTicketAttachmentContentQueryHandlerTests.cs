using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.TicketAttachments.Download;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.TicketAttachments;

public sealed class GetTicketAttachmentContentQueryHandlerTests : BaseHandlerTest
{
    private static readonly Guid CreatorId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenAttachmentDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IFileStorage fileStorage = Substitute.For<IFileStorage>();

        var handler = new GetTicketAttachmentContentQueryHandler(context, userContext, permissionProvider, fileStorage);
        var query = new GetTicketAttachmentContentQuery(Guid.NewGuid());

        // Act
        Result<TicketAttachmentResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.NotFound(query.AttachmentId));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenCallerIsNotParticipantAndLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid attachmentId = await SeedAttachmentAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var outsiderId = Guid.NewGuid();
        userContext.UserId.Returns(outsiderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(outsiderId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();

        var handler = new GetTicketAttachmentContentQueryHandler(context, userContext, permissionProvider, fileStorage);
        var query = new GetTicketAttachmentContentQuery(attachmentId);

        // Act
        Result<TicketAttachmentResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.NotFound(query.AttachmentId));
        await fileStorage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnAttachment_WhenCallerIsParticipant()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid attachmentId = await SeedAttachmentAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        Stream expectedContent = new MemoryStream([1, 2, 3]);
        fileStorage.OpenReadAsync("storage-key.pdf", Arg.Any<CancellationToken>()).Returns(expectedContent);

        var handler = new GetTicketAttachmentContentQueryHandler(context, userContext, permissionProvider, fileStorage);
        var query = new GetTicketAttachmentContentQuery(attachmentId);

        // Act
        Result<TicketAttachmentResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.FileName.ShouldBe("report.pdf");
        result.Value.ContentType.ShouldBe("application/pdf");
        result.Value.StorageKey.ShouldBe("storage-key.pdf");
        result.Value.Content.ShouldBeSameAs(expectedContent);
    }

    private static async Task<Guid> SeedAttachmentAsync(TestDbContext context)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "Original description",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            CreatedByUserId = CreatorId,
            AssignedToUserId = null,
            SectorId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };
        context.Tickets.Add(ticket);

        var attachment = new TicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            UploadedByUserId = CreatorId,
            FileName = "report.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            StorageKey = "storage-key.pdf",
            UploadedAtUtc = DateTime.UtcNow
        };
        context.TicketAttachments.Add(attachment);

        await context.SaveChangesAsync();

        return attachment.Id;
    }
}
