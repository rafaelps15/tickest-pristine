using TickestPristine.Application.Abstractions.Authentication;
using TickestPristine.Application.Abstractions.Authorization;
using TickestPristine.Application.Abstractions.Storage;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Application.TicketAttachments.Upload;
using TickestPristine.Application.UnitTests.Abstractions;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using TickestPristine.Application.TicketAttachments;
using TickestPristine.Application.Abstractions.Data;

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

        var handler = new UploadTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider, fileStorage, CreateContentInspector(), Substitute.For<ITicketActivityWriter>());
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

        var handler = new UploadTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider, fileStorage, CreateContentInspector(), Substitute.For<ITicketActivityWriter>());
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

        var handler = new UploadTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider, fileStorage, CreateContentInspector(), Substitute.For<ITicketActivityWriter>());
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

        TicketHistory history = await context.TicketHistories.SingleAsync(h => h.TicketId == attachment.TicketId);
        history.Action.ShouldBe(TicketHistoryAction.AttachmentAdded);
        history.NewValue.ShouldBe(command.FileName);
    }

    [Fact]
    public async Task Handle_Should_UploadAttachment_WhenCallerHasManagePermission()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);

        IUserContext userContext = Substitute.For<IUserContext>();
        var managerId = Guid.NewGuid();
        userContext.UserId.Returns(managerId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        permissionProvider.HasPermissionAsync(managerId, PermissionCodes.Tickets.Manage, Arg.Any<CancellationToken>())
            .Returns(true);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("generated-storage-key.pdf");

        var handler = new UploadTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider, fileStorage, CreateContentInspector(), Substitute.For<ITicketActivityWriter>());

        // Act
        Result<Guid> result = await handler.Handle(ValidCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TicketAttachment attachment = await context.TicketAttachments.SingleAsync(a => a.Id == result.Value);
        attachment.UploadedByUserId.ShouldBe(managerId);
        attachment.DomainEvents.ShouldContain(domainEvent => domainEvent is TicketAttachmentUploadedDomainEvent);
    }

    [Fact]
    public async Task Handle_Should_DeleteStoredFile_WhenSaveChangesFails()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);
        var department = Department.Create("Suporte", "Departamento de teste", responsibleUser: null);
        var existingSector = Sector.Create(department, "Helpdesk", description: null);
        context.Sectors.Add(existingSector);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IPermissionProvider permissionProvider = Substitute.For<IPermissionProvider>();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        // Depois de gravar o arquivo, um setor com chave repetida entra no contexto e faz o SaveChanges falhar.
        fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var duplicated = Sector.Create(department, "Duplicated", description: null);

                // O Id é privado na entidade: só o teste o repete, pelo EF, para provocar a chave duplicada.
                context.Entry(duplicated).Property(s => s.Id).CurrentValue = existingSector.Id;
                context.Sectors.Add(duplicated);

                return Task.FromResult("generated-storage-key.pdf");
            });

        var handler = new UploadTicketAttachmentCommandHandler(context, userContext, permissionProvider, dateTimeProvider, fileStorage, CreateContentInspector(), Substitute.For<ITicketActivityWriter>());

        // Act
        Exception? exception = await Record.ExceptionAsync(() => handler.Handle(ValidCommand(ticketId), CancellationToken.None));

        // Assert
        exception.ShouldNotBeNull();
        await fileStorage.Received(1).DeleteAsync("generated-storage-key.pdf", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnContentMismatchAndStoreNothing_WhenContentDoesNotMatchContentType()
    {
        // Arrange: executável (assinatura "MZ") declarado como PDF
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        UploadTicketAttachmentCommandHandler handler = CreateHandler(context, fileStorage, contentMatches: false);
        UploadTicketAttachmentCommand command = ValidCommand(ticketId);
        command.Content = new MemoryStream([0x4D, 0x5A, 0x90, 0x00]);

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.ContentMismatch);
        await fileStorage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await context.TicketAttachments.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnTicketLimitReached_WhenTicketAlreadyHasMaxAttachments()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);
        await SeedAttachmentsAsync(context, ticketId, TicketAttachment.MaxAttachmentsPerTicket, sizeBytes: 1024);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("key.pdf");
        UploadTicketAttachmentCommandHandler handler = CreateHandler(context, fileStorage);

        // Act
        Result<Guid> result = await handler.Handle(ValidCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.TicketLimitReached(
            TicketAttachment.MaxAttachmentsPerTicket,
            TicketAttachment.MaxTotalBytesPerTicket / (1024 * 1024)));
        // O arquivo é gravado para obter a chave e apagado quando o anexo é recusado.
        await fileStorage.Received(1).DeleteAsync("key.pdf", Arg.Any<CancellationToken>());
        (await context.TicketAttachments.CountAsync()).ShouldBe(TicketAttachment.MaxAttachmentsPerTicket);
    }

    [Fact]
    public async Task Handle_Should_ReturnTicketLimitReached_WhenTotalSizeWouldExceedLimit()
    {
        // Arrange: faltam 1.023 bytes para o limite do chamado, e o anexo novo tem 1.024
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);
        await SeedAttachmentsAsync(context, ticketId, 1, sizeBytes: TicketAttachment.MaxTotalBytesPerTicket - 1023);
        UploadTicketAttachmentCommandHandler handler = CreateHandler(context, Substitute.For<IFileStorage>());

        // Act
        Result<Guid> result = await handler.Handle(ValidCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.TicketLimitReached(
            TicketAttachment.MaxAttachmentsPerTicket,
            TicketAttachment.MaxTotalBytesPerTicket / (1024 * 1024)));
    }

    [Fact]
    public async Task Handle_Should_UploadAttachment_WhenTotalSizeReachesLimitExactly()
    {
        // Arrange: o anexo novo completa exatamente o limite do chamado
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);
        await SeedAttachmentsAsync(context, ticketId, 1, sizeBytes: TicketAttachment.MaxTotalBytesPerTicket - 1024);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("key.pdf");
        UploadTicketAttachmentCommandHandler handler = CreateHandler(context, fileStorage);

        // Act
        Result<Guid> result = await handler.Handle(ValidCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_NotCountDeletedAttachments_WhenCheckingTicketLimits()
    {
        // Arrange: o chamado tem o máximo de anexos, mas um deles foi excluído
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);
        await SeedAttachmentsAsync(context, ticketId, TicketAttachment.MaxAttachmentsPerTicket - 1, sizeBytes: 1024);
        await SeedAttachmentsAsync(context, ticketId, 1, sizeBytes: 1024, deleted: true);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("key.pdf");
        UploadTicketAttachmentCommandHandler handler = CreateHandler(context, fileStorage);

        // Act
        Result<Guid> result = await handler.Handle(ValidCommand(ticketId), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnContentMismatchAndStoreNothing_WhenContentTypeIsNotAllowed()
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        UploadTicketAttachmentCommandHandler handler = CreateHandler(context, fileStorage, contentMatches: false);
        UploadTicketAttachmentCommand command = ValidCommand(ticketId);
        command.ContentType = "application/x-msdownload";

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        // A inspeção do conteúdo, antes do armazenamento, não reconhece um tipo fora da lista.
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(TicketAttachmentErrors.ContentMismatch);
        await fileStorage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Should_ReturnExtensionMismatchAndDeleteStoredFile_WhenExtensionDoesNotMatchContentType()
    {
        // Arrange: executável declarado como PDF
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("key.pdf");
        UploadTicketAttachmentCommandHandler handler = CreateHandler(context, fileStorage);
        UploadTicketAttachmentCommand command = ValidCommand(ticketId);
        command.FileName = "nota.pdf.exe";

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Description.ShouldBe("A extensão do arquivo não corresponde ao tipo 'application/pdf'.");
        await fileStorage.Received(1).DeleteAsync("key.pdf", Arg.Any<CancellationToken>());
        (await context.TicketAttachments.AnyAsync()).ShouldBeFalse();
    }

    [Theory]
    [InlineData(10 * 1024 * 1024, true)]
    [InlineData(10 * 1024 * 1024 + 1, false)]
    public async Task Handle_Should_AcceptFilesUpToMaxSize_WhenSizeIsGiven(long fileSizeBytes, bool expectedSuccess)
    {
        // Arrange
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("chave");
        UploadTicketAttachmentCommandHandler handler = CreateHandler(context, fileStorage);
        UploadTicketAttachmentCommand command = ValidCommand(ticketId);
        command.FileSizeBytes = fileSizeBytes;

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBe(expectedSuccess);

        if (!expectedSuccess)
        {
            result.Error.Description.ShouldBe("O arquivo excede o tamanho máximo permitido de 10 MB.");
            await fileStorage.Received(1).DeleteAsync("chave", Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData("Foto.JPEG", "image/jpeg")]
    [InlineData("notas.txt", "text/plain; charset=utf-8")]
    public async Task Handle_Should_AcceptFile_WhenExtensionMatchesIgnoringCaseOrTypeHasParameters(string fileName, string contentType)
    {
        // Arrange: a extensão não diferencia maiúsculas, e alguns clientes enviam o charset junto com o tipo
        await using TestDbContext context = CreateDbContext();
        Guid ticketId = await SeedTicketAsync(context);
        IFileStorage fileStorage = Substitute.For<IFileStorage>();
        fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("chave");
        UploadTicketAttachmentCommandHandler handler = CreateHandler(context, fileStorage);
        UploadTicketAttachmentCommand command = ValidCommand(ticketId);
        command.FileName = fileName;
        command.ContentType = contentType;
        command.Content = new MemoryStream(contentType.StartsWith("image", StringComparison.Ordinal)
            ? [0xFF, 0xD8, 0xFF, 0xE0]
            : "Texto simples de teste"u8.ToArray());

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    private static UploadTicketAttachmentCommandHandler CreateHandler(
        TestDbContext context,
        IFileStorage fileStorage,
        bool contentMatches = true)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(CreatorId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTime.UtcNow);

        return new UploadTicketAttachmentCommandHandler(
            context,
            userContext,
            Substitute.For<IPermissionProvider>(),
            dateTimeProvider,
            fileStorage,
            CreateContentInspector(contentMatches),
            Substitute.For<ITicketActivityWriter>());
    }

    // A inspeção do conteúdo tem os próprios testes; aqui ela só responde se o conteúdo corresponde ao tipo.
    private static IAttachmentContentInspector CreateContentInspector(bool matches = true)
    {
        IAttachmentContentInspector inspector = Substitute.For<IAttachmentContentInspector>();
        inspector.MatchesAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(matches);

        return inspector;
    }

    private static async Task SeedAttachmentsAsync(
        TestDbContext context,
        Guid ticketId,
        int count,
        long sizeBytes,
        bool deleted = false)
    {
        for (int i = 0; i < count; i++)
        {
            context.TicketAttachments.Add(TicketTestData.CreateAttachment(ticketId, CreatorId, DateTime.UtcNow, fileName: $"anexo-{i}.pdf", fileSizeBytes: sizeBytes, storageKey: $"{Guid.NewGuid():N}.pdf", deletedAtUtc: deleted ? DateTime.UtcNow : null));
        }

        await context.SaveChangesAsync();
    }

    private static UploadTicketAttachmentCommand ValidCommand(Guid ticketId) => new()
    {
        TicketId = ticketId,
        FileName = "report.pdf",
        ContentType = "application/pdf",
        FileSizeBytes = 1024,
        // Começa com a assinatura de PDF: o conteúdo é conferido contra o tipo declarado.
        Content = new MemoryStream("%PDF-1.7 conteúdo de teste"u8.ToArray())
    };

    private static async Task<Guid> SeedTicketAsync(TestDbContext context)
    {
        Ticket ticket = TicketTestData.Create(requesterId: CreatorId, description: "Original description");

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }
}
