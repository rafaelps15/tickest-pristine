using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.TicketAttachments.Delete;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.TicketAttachments;

public sealed class DeleteTicketAttachmentCommandHandlerTests : BaseHandlerTest
{
    private static readonly Guid UploaderId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenAttachmentDoesNotExist()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UploaderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new DeleteTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketAttachmentCommand(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.NotFound(command.AttachmentId));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTicketIsDeleted()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        // O anexo continua ativo, mas o chamado dele foi excluído logicamente.
        Guid attachmentId = await SeedAttachmentAsync(context, ticketDeleted: true);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UploaderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new DeleteTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketAttachmentCommand(attachmentId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.NotFound(attachmentId));
    }

    [Fact]
    public async Task Handle_Should_ReturnUnauthorized_WhenCallerIsNotUploaderAndLacksManagePermission()
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
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new DeleteTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketAttachmentCommand(attachmentId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UserErrors.Unauthorized());
    }

    [Fact]
    public async Task Handle_Should_SoftDeleteAttachmentAndRaiseDomainEvent_WhenCallerIsUploader()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid attachmentId = await SeedAttachmentAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UploaderId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new DeleteTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketAttachmentCommand(attachmentId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TicketAttachment attachment = await context.TicketAttachments
            .IgnoreQueryFilters()
            .SingleAsync(a => a.Id == attachmentId);
        attachment.DeletedAtUtc.ShouldNotBeNull();
        attachment.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketAttachmentDeletedDomainEvent);
        (await context.TicketHistories.SingleAsync(h => h.TicketId == attachment.TicketId)).Action.ShouldBe(TicketHistoryAction.AttachmentRemoved);
    }

    [Fact]
    public async Task Handle_Should_SoftDeleteAttachment_WhenCallerHasManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid attachmentId = await SeedAttachmentAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var managerId = Guid.NewGuid();
        userContext.UserId.Returns(managerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(managerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        var handler = new DeleteTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider);
        var command = new DeleteTicketAttachmentCommand(attachmentId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TicketAttachment attachment = await context.TicketAttachments
            .IgnoreQueryFilters()
            .SingleAsync(a => a.Id == attachmentId);
        attachment.DeletedAtUtc.ShouldNotBeNull();
        attachment.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketAttachmentDeletedDomainEvent);
    }

    private static async Task<Guid> SeedAttachmentAsync(TestDbContext context, bool ticketDeleted = false)
    {
        Ticket ticket = TicketTestData.Create(
            requesterId: UploaderId,
            description: "Original description",
            deletedAtUtc: ticketDeleted ? DateTime.UtcNow : null);
        context.Tickets.Add(ticket);

        var attachment = new TicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            UploadedByUserId = UploaderId,
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
