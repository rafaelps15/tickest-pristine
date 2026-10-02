using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Authorization;
using TickestPristine.Application.TicketAttachments.Upload;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;
using TickestPristine.Application.TicketAttachments;

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
        result.Error.ShouldBe(TicketErrors.NotFound(command.TicketId));
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
        result.Error.ShouldBe(UserErrors.Unauthorized());
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
        await fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_DeleteStoredFile_WhenAttachmentIsNotSaved()
    {
        // Arrange: o cancelamento chega depois de gravar o arquivo, então o SaveChanges falha
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);
        using var cancellation = new CancellationTokenSource();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await cancellation.CancelAsync();
                return "orphan-storage-key.pdf";
            });

        var handler = new UploadTicketAttachmentCommandHandler(
            context,
            userContext,
            Substitute.For<IPermissionProvider>(),
            dateTimeProvider,
            fileStorage);

        // Act
        Exception? exception = await Record.ExceptionAsync(() => handler.Handle(ValidCommand(ticketId), cancellation.Token));

        // Assert
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        await fileStorage.Received(1).DeleteAsync("orphan-storage-key.pdf", Arg.Any<CancellationToken>());
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
        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
