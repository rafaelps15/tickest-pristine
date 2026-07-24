using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.TicketAttachments.Upload;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Application.UnitTests.TicketAttachments;

public sealed class UploadTicketAttachmentCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid CreatorId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        IFileStorage fileStorage = Substitute.For<IFileStorage>();

        var handler = new UploadTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider, fileStorage);
        UploadTicketAttachmentCommand command = ValidCommand(Guid.NewGuid());

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tickets.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotParticipantAndLacksManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var outsiderId = Guid.NewGuid();
        userContext.UserId.Returns(outsiderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(outsiderId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        IFileStorage fileStorage = Substitute.For<IFileStorage>();

        var handler = new UploadTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider, fileStorage);
        UploadTicketAttachmentCommand command = ValidCommand(ticketId);

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Tickets.Unauthorized");
    }

    [Fact]
    public async Task Handle_Should_ReturnFileTooLarge_WhenFileExceedsMaxSize()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        IFileStorage fileStorage = Substitute.For<IFileStorage>();

        var handler = new UploadTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider, fileStorage);
        UploadTicketAttachmentCommand command = ValidCommand(ticketId);
        command.FileSizeBytes = 11 * 1024 * 1024;

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TicketAttachments.FileTooLarge");
    }

    [Fact]
    public async Task Handle_Should_ReturnUnsupportedContentType_WhenContentTypeIsNotAllowed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        IFileStorage fileStorage = Substitute.For<IFileStorage>();

        var handler = new UploadTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider, fileStorage);
        UploadTicketAttachmentCommand command = ValidCommand(ticketId);
        command.ContentType = "application/x-msdownload";

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TicketAttachments.UnsupportedContentType");
    }

    [Fact]
    public async Task Handle_Should_UploadAttachmentAndRaiseDomainEvent_WhenValid()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("generated-storage-key.pdf");

        var handler = new UploadTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider, fileStorage);
        UploadTicketAttachmentCommand command = ValidCommand(ticketId);

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TicketAttachment attachment = await context.TicketAttachments.SingleAsync(a => a.Id == result.Value);
        attachment.TicketId.ShouldBe(ticketId);
        attachment.UploadedByUserId.ShouldBe(CreatorId);
        attachment.StorageKey.ShouldBe("generated-storage-key.pdf");
        attachment.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketAttachmentUploadedDomainEvent);
    }

    private static UploadTicketAttachmentCommand ValidCommand(Guid ticketId) => new()
    {
        TicketId = ticketId,
        FileName = "report.pdf",
        ContentType = "application/pdf",
        FileSizeBytes = 1024,
        Content = new MemoryStream([1, 2, 3])
    };

    private static async Task<Guid> SeedTicketAsync(TestDbContext context)
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
        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
