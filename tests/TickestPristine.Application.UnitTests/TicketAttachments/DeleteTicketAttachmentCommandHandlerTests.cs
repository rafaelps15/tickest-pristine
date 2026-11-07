using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.TicketAttachments.Delete;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.Domain.TicketAttachments;
using TickestPristine.Domain.TicketHistories;

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

    [Fact]
    public async Task Handle_Should_RecordRemovalWithoutChangingTicket_WhenAttachmentIsDeleted()
    {
        // Arrange
        // Depois da abertura dos chamados do arranjo, que usam a hora real.
        DateTime nowUtc = DateTime.UtcNow.AddHours(1);
        await using TestDbContext context = CreateDbContext();
        Guid attachmentId = await SeedAttachmentAsync(context);
        Ticket before = await context.Tickets.AsNoTracking().SingleAsync();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UploaderId);
        var handler = new DeleteTicketAttachmentCommandHandler(
            context,
            userContext,
            Substitute.For<IPermissionProvider>(),
            CreateClock(nowUtc));

        // Act
        Result result = await handler.Handle(new DeleteTicketAttachmentCommand(attachmentId), CancellationToken.None);

        // Assert: a última atividade fica para a projeção, que o evento da exclusão dispara depois do commit.
        result.IsSuccess.ShouldBeTrue();
        Ticket stored = await context.Tickets.AsNoTracking().SingleAsync();
        stored.LastActivityAtUtc.ShouldBe(before.LastActivityAtUtc);
        TicketHistoryEntry removal = await context.TicketHistories.SingleAsync();
        removal.Action.ShouldBe(TicketHistoryAction.AttachmentRemoved);
        removal.ChangedByUserId.ShouldBe(UploaderId);
    }

    private static async Task<Guid> SeedAttachmentAsync(TestDbContext context, bool ticketDeleted = false)
    {
        Ticket ticket = TicketTestData.Create(
            requesterId: UploaderId,
            description: "Original description",
            deletedAtUtc: ticketDeleted ? DateTime.UtcNow : null);
        context.Tickets.Add(ticket);

        TicketAttachment attachment = TicketTestData.CreateAttachment(ticket.Id, UploaderId, DateTime.UtcNow);
        context.TicketAttachments.Add(attachment);

        await context.SaveChangesAsync();

        return attachment.Id;
    }
}
