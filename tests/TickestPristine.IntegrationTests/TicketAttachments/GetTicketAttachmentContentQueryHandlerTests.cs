using TickestPristine.Application.Tickets;
using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.TicketAttachments.GetContent;
using TickestPristine.IntegrationTests.Abstractions;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TickestPristine.SharedKernel;
using TickestPristine.Domain.TicketAttachments;

namespace TickestPristine.IntegrationTests.TicketAttachments;

public sealed class GetTicketAttachmentContentQueryHandlerTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    private static readonly Guid CreatorId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenAttachmentDoesNotExist()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IFileStorage fileStorage = Substitute.For<IFileStorage>();

        var handler = new GetTicketAttachmentContentQueryHandler(DbConnectionFactory,
            userContext,
            new TicketViewAccess(permissionProvider),
            fileStorage,
            NullLogger<GetTicketAttachmentContentQueryHandler>.Instance);
        var query = new GetTicketAttachmentContentQuery(Guid.NewGuid());

        // Act
        Result<TicketAttachmentResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.NotFound(query.AttachmentId));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketIsDeleted()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        Guid attachmentId = await SeedAttachmentAsync(context);
        Ticket ticket = await context.Tickets.SingleAsync();
        new TicketSlaCycleService().Delete(ticket, [], CreatorId, DateTime.UtcNow);
        ticket.ClearDomainEvents();
        await context.SaveChangesAsync();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IFileStorage fileStorage = Substitute.For<IFileStorage>();

        var handler = new GetTicketAttachmentContentQueryHandler(DbConnectionFactory,
            userContext,
            new TicketViewAccess(permissionProvider),
            fileStorage,
            NullLogger<GetTicketAttachmentContentQueryHandler>.Instance);
        var query = new GetTicketAttachmentContentQuery(attachmentId);

        // Act
        Result<TicketAttachmentResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.NotFound(query.AttachmentId));
        await fileStorage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotParticipantAndLacksManagePermission()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        Guid attachmentId = await SeedAttachmentAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var outsiderId = Guid.NewGuid();
        userContext.UserId.Returns(outsiderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(outsiderId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(false);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();

        var handler = new GetTicketAttachmentContentQueryHandler(DbConnectionFactory,
            userContext,
            new TicketViewAccess(permissionProvider),
            fileStorage,
            NullLogger<GetTicketAttachmentContentQueryHandler>.Instance);
        var query = new GetTicketAttachmentContentQuery(attachmentId);

        // Act
        Result<TicketAttachmentResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
        await fileStorage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnAttachment_WhenCallerIsParticipant()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        Guid attachmentId = await SeedAttachmentAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        Stream expectedContent = new MemoryStream([1, 2, 3]);
        fileStorage.OpenReadAsync("storage-key.pdf", Arg.Any<CancellationToken>()).Returns(expectedContent);

        var handler = new GetTicketAttachmentContentQueryHandler(DbConnectionFactory,
            userContext,
            new TicketViewAccess(permissionProvider),
            fileStorage,
            NullLogger<GetTicketAttachmentContentQueryHandler>.Instance);
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

    [Fact]
    public async Task Handle_Should_ReturnAttachment_WhenCallerHasManagePermission()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        Guid attachmentId = await SeedAttachmentAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var managerId = Guid.NewGuid();
        userContext.UserId.Returns(managerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(managerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        Stream expectedContent = new MemoryStream([1, 2, 3]);
        fileStorage.OpenReadAsync("storage-key.pdf", Arg.Any<CancellationToken>()).Returns(expectedContent);

        var handler = new GetTicketAttachmentContentQueryHandler(DbConnectionFactory,
            userContext,
            new TicketViewAccess(permissionProvider),
            fileStorage,
            NullLogger<GetTicketAttachmentContentQueryHandler>.Instance);
        var query = new GetTicketAttachmentContentQuery(attachmentId);

        // Act
        Result<TicketAttachmentResponse> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Content.ShouldBeSameAs(expectedContent);
    }

    [Fact]
    public async Task Handle_Should_ReturnFileMissingAndLogError_WhenFileIsNotInStorage()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        Guid attachmentId = await SeedAttachmentAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        fileStorage.OpenReadAsync("storage-key.pdf", Arg.Any<CancellationToken>()).Returns((Stream?)null);
        var logger = new TestLogger<GetTicketAttachmentContentQueryHandler>();

        var handler = new GetTicketAttachmentContentQueryHandler(DbConnectionFactory,
            userContext,
            new TicketViewAccess(Substitute.For<IPermissionProvider>()),
            fileStorage,
            logger);

        // Act
        Result<TicketAttachmentResponse> result = await handler.Handle(
            new GetTicketAttachmentContentQuery(attachmentId),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.FileMissing(attachmentId));

        TestLogEntry error = logger.Entries.ShouldHaveSingleItem();
        error.Level.ShouldBe(LogLevel.Error);
        error.Message.ShouldContain(attachmentId.ToString());
    }

    private static async Task<Guid> SeedAttachmentAsync(ApplicationDbContext context)
    {
        Ticket ticket = QueryTicketTestData.Create(requesterId: CreatorId, description: "Original description");
        context.Tickets.Add(ticket);

        TicketAttachment attachment = QueryTicketTestData.CreateAttachment(ticket.Id, CreatorId, DateTime.UtcNow);
        context.TicketAttachments.Add(attachment);

        await context.SaveChangesAsync();

        return attachment.Id;
    }
}
