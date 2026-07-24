using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.TicketAttachments.Download;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.TicketAttachments;

public sealed class DownloadTicketAttachmentQueryHandlerTests : BaseHandlerTest
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

        var handler = new DownloadTicketAttachmentQueryHandler(context, userContext, permissionProvider, fileStorage);
        var query = new DownloadTicketAttachmentQuery(Guid.NewGuid());

        // Act
        Result<TicketAttachmentDownloadResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TicketAttachments.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotParticipantAndLacksManagePermission()
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

        var handler = new DownloadTicketAttachmentQueryHandler(context, userContext, permissionProvider, fileStorage);
        var query = new DownloadTicketAttachmentQuery(attachmentId);

        // Act
        Result<TicketAttachmentDownloadResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tickets.Unauthorized");
    }

    [Fact]
    public async Task Handle_Should_ReturnFileContent_WhenCallerIsParticipant()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid attachmentId = await SeedAttachmentAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        Stream expectedStream = new MemoryStream([1, 2, 3]);
        fileStorage.OpenReadAsync("storage-key.pdf", Arg.Any<CancellationToken>()).Returns(expectedStream);

        var handler = new DownloadTicketAttachmentQueryHandler(context, userContext, permissionProvider, fileStorage);
        var query = new DownloadTicketAttachmentQuery(attachmentId);

        // Act
        Result<TicketAttachmentDownloadResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.FileName.ShouldBe("report.pdf");
        result.Value.ContentType.ShouldBe("application/pdf");
        result.Value.Content.ShouldBeSameAs(expectedStream);
    }

    private static async Task<Guid> SeedAttachmentAsync(TestDbContext context)
    {
        var ticket = Ticket.Create(
            "Printer is broken",
            "Original description",
            TicketPriority.Medium,
            CreatorId,
            null,
            Guid.NewGuid(),
            DateTime.UtcNow);
        context.Tickets.Add(ticket);

        var attachment = TicketAttachment.Create(
            ticket.Id,
            CreatorId,
            "report.pdf",
            "application/pdf",
            1024,
            "storage-key.pdf",
            DateTime.UtcNow);
        context.TicketAttachments.Add(attachment);

        await context.SaveChangesAsync();

        return attachment.Id;
    }
}
