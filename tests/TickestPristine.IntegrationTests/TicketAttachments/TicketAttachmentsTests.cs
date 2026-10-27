using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Infrastructure.Storage;
using TickestPristine.IntegrationTests.Tickets;

namespace TickestPristine.IntegrationTests.TicketAttachments;

public sealed class TicketAttachmentsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const string TooLargeDetail = "O arquivo excede o tamanho máximo permitido para anexos. O limite é de 10 MB.";

    private readonly IntegrationTestWebAppFactory _factory = factory;

    private async Task AuthenticateAsAdminAsync()
    {
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);
    }

    private async Task<Guid> CreateSectorAsAdminAsync()
    {
        await AuthenticateAsAdminAsync();

        HttpResponseMessage departmentResponse = await HttpClient.PostAsJsonAsync("departments", new
        {
            name = $"Department-{Guid.NewGuid():N}",
            description = "A department created by tests"
        });
        departmentResponse.EnsureSuccessStatusCode();
        Guid departmentId = await departmentResponse.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage sectorResponse = await HttpClient.PostAsJsonAsync("sectors", new
        {
            name = $"Sector-{Guid.NewGuid():N}",
            description = "A sector created by tests",
            departmentId
        });
        sectorResponse.EnsureSuccessStatusCode();

        return await sectorResponse.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<Guid> CreateTicketAsync(Guid sectorId)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = "Printer is broken",
            description = "The printer on the 3rd floor is not working",
            priority = 2,
            sectorId
        });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    // O conteúdo retornado fica responsável por liberar o arquivo.
#pragma warning disable CA2000
    private static MultipartFormDataContent BuildFileContent(
        string fileName = "report.pdf",
        string contentType = "application/pdf",
        string fileText = "%PDF-1.7 test file content") =>
        BuildFileContent(fileName, contentType, Encoding.UTF8.GetBytes(fileText));

    private static MultipartFormDataContent BuildFileContent(string fileName, string contentType, byte[] bytes)
    {
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return new MultipartFormDataContent { { fileContent, "file", fileName } };
    }
#pragma warning restore CA2000

    private async Task<Guid> UploadAttachmentAsync(
        Guid ticketId,
        string fileName = "report.pdf",
        string contentType = "application/pdf",
        string fileText = "%PDF-1.7 test file content")
    {
        using MultipartFormDataContent content = BuildFileContent(fileName, contentType, fileText);
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{ticketId}/attachments", content);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<HttpResponseMessage> PostAttachmentAsync(Guid ticketId, string fileName, string contentType, byte[] bytes)
    {
        using MultipartFormDataContent content = BuildFileContent(fileName, contentType, bytes);

        return await HttpClient.PostAsync($"tickets/{ticketId}/attachments", content);
    }

    /// <summary>
    /// Chamado novo de um Colaborador novo, que fica autenticado.
    /// </summary>
    private async Task<(Guid TicketId, AccessTokens Tokens)> CreateTicketAsCollaboratorAsync()
    {
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        return (await CreateTicketAsync(sectorId), tokens);
    }

    [Fact]
    public async Task Upload_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        using MultipartFormDataContent content = BuildFileContent();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{Guid.NewGuid()}/attachments", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        using MultipartFormDataContent content = BuildFileContent();
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{Guid.NewGuid()}/attachments", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Upload_Should_ReturnBadRequest_WhenContentTypeIsNotAllowed()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        using MultipartFormDataContent content = BuildFileContent(fileName: "malware.exe", contentType: "application/x-msdownload");
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{ticketId}/attachments", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_Should_ReturnUploadedAttachmentInGetAll_WhenCallerIsTicketCreator()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        Guid attachmentId = await UploadAttachmentAsync(ticketId);

        // Assert
        attachmentId.ShouldNotBe(Guid.Empty);

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"tickets/{ticketId}/attachments");
        getResponse.EnsureSuccessStatusCode();
        List<TicketAttachmentDto>? attachments = (await getResponse.Content.ReadFromJsonAsync<CursorPageDto<TicketAttachmentDto>>())?.Items;
        attachments!.ShouldContain(a => a.Id == attachmentId && a.FileName == "report.pdf");
    }

    [Fact]
    public async Task Download_Should_ReturnUploadedFileContent_WhenCallerIsTicketCreator()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        Guid attachmentId = await UploadAttachmentAsync(
            ticketId,
            fileName: "notes.txt",
            contentType: "text/plain",
            fileText: "hello from the attachment");

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"ticket-attachments/{attachmentId}/download");

        // Assert
        response.EnsureSuccessStatusCode();
        string downloaded = await response.Content.ReadAsStringAsync();
        downloaded.ShouldBe("hello from the attachment");
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
    }

    [Fact]
    public async Task Upload_Should_ReturnBadRequest_WhenExtensionDoesNotMatchContentType()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act: executável declarado como PDF
        using MultipartFormDataContent content = BuildFileContent(fileName: "nota.pdf.exe");
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{ticketId}/attachments", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Download_Should_ReturnForbidden_WhenCallerIsNotParticipantAndLacksManagePermission()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        Guid attachmentId = await UploadAttachmentAsync(ticketId);

        (_, AccessTokens outsiderTokens) = await RegisterAndLoginAsync();
        Authenticate(outsiderTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"ticket-attachments/{attachmentId}/download");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_Should_RemoveAttachment_WhenCallerIsUploader()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        Guid attachmentId = await UploadAttachmentAsync(ticketId);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"ticket-attachments/{attachmentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"tickets/{ticketId}/attachments");
        getResponse.EnsureSuccessStatusCode();
        List<TicketAttachmentDto>? attachments = (await getResponse.Content.ReadFromJsonAsync<CursorPageDto<TicketAttachmentDto>>())?.Items;
        attachments!.ShouldNotContain(a => a.Id == attachmentId);
    }

    [Fact]
    public async Task Delete_Should_ReturnNotFound_WhenAttachmentDoesNotExist()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"ticket-attachments/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_Should_ReturnForbidden_WhenCallerIsNotParticipantAndLacksManagePermission()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        (_, AccessTokens outsiderTokens) = await RegisterAndLoginAsync();
        Authenticate(outsiderTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{ticketId}/attachments");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_Should_ReturnForbidden_WhenCallerIsNotParticipantAndLacksManagePermission()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        (_, AccessTokens outsiderTokens) = await RegisterAndLoginAsync();
        Authenticate(outsiderTokens.AccessToken);

        // Act
        using MultipartFormDataContent content = BuildFileContent();
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{ticketId}/attachments", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_Should_ReturnPayloadTooLargeProblem_WhenBodyExceedsLimit()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsCollaboratorAsync();
        int filesBefore = CountStoredFiles();

        // Act: 12 MB passam do limite de 11 MB do corpo da requisição
        using MultipartFormDataContent content = BuildFileContent(fileText: "%PDF-" + new string('a', 12 * 1024 * 1024));
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{ticketId}/attachments", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Status.ShouldBe(413);
        problem.Detail.ShouldBe(TooLargeDetail);
        CountStoredFiles().ShouldBe(filesBefore);
    }

    [Fact]
    public async Task Download_Should_ReturnNotFound_WhenAttachmentDoesNotExist()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"ticket-attachments/{Guid.NewGuid()}/download");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{Guid.NewGuid()}/attachments");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Should_ReturnForbidden_WhenCallerIsNotUploaderAndLacksManagePermission()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        Guid attachmentId = await UploadAttachmentAsync(ticketId);

        (_, AccessTokens outsiderTokens) = await RegisterAndLoginAsync();
        Authenticate(outsiderTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"ticket-attachments/{attachmentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_Should_AcceptOnlyContentMatchingDeclaredType()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsCollaboratorAsync();
        byte[] executable = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        byte[] textWithNullBytes = [0x41, 0x42, 0x00, 0x00, 0x43];
        int filesBefore = CountStoredFiles();

        // Act
        HttpResponseMessage renamedExecutable = await PostAttachmentAsync(ticketId, "foto.png", "image/png", executable);
        int filesAfterRenamedExecutable = CountStoredFiles();
        HttpResponseMessage realPng = await PostAttachmentAsync(ticketId, "foto.png", "image/png", png);
        HttpResponseMessage renamedZip = await PostAttachmentAsync(ticketId, "planilha.xlsx", Xlsx, Package("dados.csv"));
        int filesAfterRenamedZip = CountStoredFiles();
        HttpResponseMessage realXlsx = await PostAttachmentAsync(ticketId, "planilha.xlsx", Xlsx, Package("xl/workbook.xml"));
        HttpResponseMessage binaryText = await PostAttachmentAsync(ticketId, "notas.txt", "text/plain", textWithNullBytes);

        // Assert
        await ShouldBeProblemAsync(renamedExecutable, HttpStatusCode.BadRequest, "TicketAttachments.ContentMismatch");
        filesAfterRenamedExecutable.ShouldBe(filesBefore);
        realPng.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ShouldBeProblemAsync(renamedZip, HttpStatusCode.BadRequest, "TicketAttachments.ContentMismatch");
        filesAfterRenamedZip.ShouldBe(filesBefore + 1);
        realXlsx.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ShouldBeProblemAsync(binaryText, HttpStatusCode.BadRequest, "TicketAttachments.ContentMismatch");

        CountStoredFiles().ShouldBe(filesBefore + 2);
        (await GetStoredAttachmentsAsync(ticketId)).Select(a => a.FileName).Order().ShouldBe(["foto.png", "planilha.xlsx"]);
    }

    [Fact]
    public async Task Upload_Should_RejectFileOneByteOverMaxSize_WithoutStoringIt()
    {
        // Arrange: 10 MB + 1 byte cabem no corpo da requisição (11 MB), mas passam do limite do arquivo
        (Guid ticketId, _) = await CreateTicketAsCollaboratorAsync();
        byte[] bytes = new byte[10 * 1024 * 1024 + 1];
        "%PDF-1.7"u8.CopyTo(bytes);
        int filesBefore = CountStoredFiles();

        // Act
        HttpResponseMessage response = await PostAttachmentAsync(ticketId, "grande.pdf", "application/pdf", bytes);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("O arquivo excede o tamanho máximo permitido de 10 MB.");
        CountStoredFiles().ShouldBe(filesBefore);
        (await GetStoredAttachmentsAsync(ticketId)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("../../a.png")]
    [InlineData("alerta\u0007.png")]
    public async Task Upload_Should_RejectFileNameWithPathOrControlCharacters(string fileName)
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsCollaboratorAsync();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

        // Act
        HttpResponseMessage response = await PostAttachmentAsync(ticketId, fileName, "image/png", png);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("O nome do arquivo contém caracteres inválidos.");
        (await GetStoredAttachmentsAsync(ticketId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPolicy_Should_ReturnLimitsAndAllowedTypes()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("ticket-attachments/policy");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PolicyDto? policy = await response.Content.ReadFromJsonAsync<PolicyDto>();
        policy!.MaxFileSizeBytes.ShouldBe(10_485_760);
        policy.MaxAttachmentsPerTicket.ShouldBe(20);
        policy.MaxTotalBytesPerTicket.ShouldBe(104_857_600);
        policy.AllowedTypes.Select(t => t.ContentType).ShouldBe(
        [
            "image/png",
            "image/jpeg",
            "image/gif",
            "image/webp",
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            Xlsx,
            "text/plain",
            "application/zip"
        ]);
        policy.AllowedTypes.Single(t => t.ContentType == "image/jpeg").Extensions.ShouldBe([".jpg", ".jpeg"]);
        policy.AllowedTypes.Single(t => t.ContentType == Xlsx).Extensions.ShouldBe([".xlsx"]);
    }

    [Fact]
    public async Task GetPolicy_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange: o cliente não tem token
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("ticket-attachments/policy");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_Should_ReturnTicketLimitReached_WhenTicketHasMaxAttachmentsOrMaxTotalSize()
    {
        // Arrange: um chamado com 20 anexos e outro a meio MB do limite de 100 MB somados
        (Guid fullCountTicketId, AccessTokens tokens) = await CreateTicketAsCollaboratorAsync();
        Guid userId = GetUserId(tokens);
        List<Guid> existing = await InsertAttachmentsAsync(fullCountTicketId, userId, count: 20, sizeBytes: 1024);

        Guid sectorId = await CreateSectorAsAdminAsync();
        Authenticate(tokens.AccessToken);
        Guid fullSizeTicketId = await CreateTicketAsync(sectorId);
        await InsertAttachmentsAsync(fullSizeTicketId, userId, count: 1, sizeBytes: 100 * 1024 * 1024 - 512 * 1024);

        byte[] oneMegabytePdf = new byte[1024 * 1024];
        "%PDF-1.7"u8.CopyTo(oneMegabytePdf);

        // Act
        HttpResponseMessage overCount = await PostAttachmentAsync(fullCountTicketId, "a.pdf", "application/pdf", oneMegabytePdf);
        HttpResponseMessage overSize = await PostAttachmentAsync(fullSizeTicketId, "a.pdf", "application/pdf", oneMegabytePdf);
        (await HttpClient.DeleteAsync($"ticket-attachments/{existing[0]}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        HttpResponseMessage afterDeletion = await PostAttachmentAsync(fullCountTicketId, "a.pdf", "application/pdf", oneMegabytePdf);

        // Assert
        await ShouldBeProblemAsync(overCount, HttpStatusCode.BadRequest, "TicketAttachments.TicketLimitReached");
        await ShouldBeProblemAsync(overSize, HttpStatusCode.BadRequest, "TicketAttachments.TicketLimitReached");
        afterDeletion.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetStoredAttachmentsAsync(fullCountTicketId)).Count.ShouldBe(20);
        (await GetStoredAttachmentsAsync(fullSizeTicketId)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Download_Should_ReturnFileMissing_WhenFileIsNotInStorage()
    {
        // Arrange: a linha continua no banco, mas o arquivo foi apagado do disco
        (Guid ticketId, _) = await CreateTicketAsCollaboratorAsync();
        Guid attachmentId = await UploadAttachmentAsync(ticketId);
        File.Delete(StoragePath((await GetStoredAttachmentAsync(attachmentId)).StorageKey));

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"ticket-attachments/{attachmentId}/download");

        // Assert
        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "TicketAttachments.FileMissing");
    }

    [Fact]
    public async Task Download_Should_SendAttachmentContentDispositionAndNoSniff()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsCollaboratorAsync();
        Guid attachmentId = await UploadAttachmentAsync(ticketId, fileName: "relatório final.pdf");

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"ticket-attachments/{attachmentId}/download");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ContentDispositionHeaderValue disposition = response.Content.Headers.ContentDisposition!;
        disposition.DispositionType.ShouldBe("attachment");
        disposition.FileNameStar.ShouldBe("relatório final.pdf");
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
    }

    [Fact]
    public async Task Delete_Should_RemoveStoredFileAndSetPurgedAt_EvenWhenFileIsAlreadyGone()
    {
        // Arrange: um anexo com o arquivo no disco e outro cujo arquivo já sumiu
        (Guid ticketId, _) = await CreateTicketAsCollaboratorAsync();
        Guid attachmentId = await UploadAttachmentAsync(ticketId);
        string path = StoragePath((await GetStoredAttachmentAsync(attachmentId)).StorageKey);
        File.Exists(path).ShouldBeTrue();

        Guid missingFileAttachmentId = await UploadAttachmentAsync(ticketId);
        File.Delete(StoragePath((await GetStoredAttachmentAsync(missingFileAttachmentId)).StorageKey));

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"ticket-attachments/{attachmentId}");
        HttpResponseMessage missingFileResponse = await HttpClient.DeleteAsync($"ticket-attachments/{missingFileAttachmentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        File.Exists(path).ShouldBeFalse();
        TicketAttachment deleted = await GetStoredAttachmentAsync(attachmentId);
        deleted.DeletedAtUtc.ShouldNotBeNull();
        deleted.PurgedAtUtc.ShouldNotBeNull();

        missingFileResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetStoredAttachmentAsync(missingFileAttachmentId)).PurgedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task Delete_Should_KeepCurrentRules_ForOtherUserUploaderRepeatedDeletionListAndDownload()
    {
        // Arrange: o anexo é do Colaborador A; B não participa do chamado nem tem tickets:manage
        (Guid ticketId, AccessTokens uploaderTokens) = await CreateTicketAsCollaboratorAsync();
        Guid attachmentId = await UploadAttachmentAsync(ticketId);
        (_, AccessTokens otherTokens) = await RegisterAndLoginAsync();

        // Act
        Authenticate(otherTokens.AccessToken);
        HttpResponseMessage byOther = await HttpClient.DeleteAsync($"ticket-attachments/{attachmentId}");

        Authenticate(uploaderTokens.AccessToken);
        HttpResponseMessage byUploader = await HttpClient.DeleteAsync($"ticket-attachments/{attachmentId}");
        HttpResponseMessage again = await HttpClient.DeleteAsync($"ticket-attachments/{attachmentId}");
        CursorPageDto<TicketAttachmentDto>? list =
            await HttpClient.GetFromJsonAsync<CursorPageDto<TicketAttachmentDto>>($"tickets/{ticketId}/attachments");
        HttpResponseMessage download = await HttpClient.GetAsync($"ticket-attachments/{attachmentId}/download");

        // Assert
        byOther.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        byUploader.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        list!.Items.ShouldNotContain(a => a.Id == attachmentId);
        download.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.TicketHistories.CountAsync(h =>
            h.TicketId == ticketId && h.Action == TicketHistoryAction.AttachmentRemoved)).ShouldBe(1);
    }

    [Fact]
    public async Task Upload_Should_ReturnTooManyRequests_WhenUploadLimitIsExceeded()
    {
        // Arrange: a API com o limite padrão de envio (30 a cada 10 minutos por usuário)
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        using var limitedFactory = new WebApplicationFactoryWithSetting(_factory, "RateLimiting:AttachmentUpload:PermitLimit", "30");
        using HttpClient client = limitedFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var statusCodes = new List<HttpStatusCode>();

        // Act: chamado inexistente, para não gravar nada; o limite conta a requisição
        for (int i = 0; i < 31; i++)
        {
            using MultipartFormDataContent content = BuildFileContent();
            using HttpResponseMessage response = await client.PostAsync($"tickets/{Guid.NewGuid()}/attachments", content);
            statusCodes.Add(response.StatusCode);
        }

        // Assert
        statusCodes.Take(30).ShouldAllBe(statusCode => statusCode == HttpStatusCode.NotFound);
        statusCodes[30].ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Delete_Should_ReturnTooManyRequests_WhenDeleteLimitIsExceeded()
    {
        // Arrange: a API com o limite padrão de exclusão (60 por minuto por usuário)
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        using var limitedFactory = new WebApplicationFactoryWithSetting(_factory, "RateLimiting:AttachmentDelete:PermitLimit", "60");
        using HttpClient client = limitedFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var statusCodes = new List<HttpStatusCode>();

        // Act
        for (int i = 0; i < 61; i++)
        {
            using HttpResponseMessage response = await client.DeleteAsync($"ticket-attachments/{Guid.NewGuid()}");
            statusCodes.Add(response.StatusCode);
        }

        // Assert
        statusCodes.Take(60).ShouldAllBe(statusCode => statusCode == HttpStatusCode.NotFound);
        statusCodes[60].ShouldBe(HttpStatusCode.TooManyRequests);
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        (await response.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe(code);
    }

    private string StorageRootPath =>
        Path.GetFullPath(Services.GetRequiredService<IOptions<FileStorageOptions>>().Value.RootPath);

    private string StoragePath(string storageKey) => Path.Combine(StorageRootPath, storageKey);

    private int CountStoredFiles() =>
        Directory.Exists(StorageRootPath) ? Directory.GetFiles(StorageRootPath).Length : 0;

    private async Task<TicketAttachment> GetStoredAttachmentAsync(Guid attachmentId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.TicketAttachments.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == attachmentId);
    }

    private async Task<List<TicketAttachment>> GetStoredAttachmentsAsync(Guid ticketId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.TicketAttachments.AsNoTracking().Where(a => a.TicketId == ticketId).ToListAsync();
    }

    /// <summary>
    /// Anexos gravados direto no banco, sem arquivo: os limites do chamado contam as linhas.
    /// </summary>
    private async Task<List<Guid>> InsertAttachmentsAsync(Guid ticketId, Guid userId, int count, long sizeBytes)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var attachments = Enumerable.Range(0, count)
            .Select(i => new TicketAttachment
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                UploadedByUserId = userId,
                FileName = $"anexo-{i}.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = sizeBytes,
                StorageKey = $"{Guid.NewGuid():N}.pdf",
                UploadedAtUtc = DateTime.UtcNow
            })
            .ToList();

        context.TicketAttachments.AddRange(attachments);
        await context.SaveChangesAsync();

        return attachments.Select(a => a.Id).ToList();
    }

    /// <summary>
    /// Pacote ZIP com uma entrada no caminho informado.
    /// </summary>
    private static byte[] Package(string entryName)
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName);
            using StreamWriter writer = new(entry.Open());
            writer.Write("<conteudo/>");
        }

        return buffer.ToArray();
    }

    private sealed record PolicyDto(
        long MaxFileSizeBytes,
        int MaxAttachmentsPerTicket,
        long MaxTotalBytesPerTicket,
        List<AllowedTypeDto> AllowedTypes);

    private sealed record AllowedTypeDto(string ContentType, List<string> Extensions);

    private sealed record ProblemDto(int Status, string Title, string Detail);

    private sealed record TicketAttachmentDto(
        Guid Id,
        Guid TicketId,
        Guid UploadedByUserId,
        string FileName,
        string ContentType,
        long FileSizeBytes);
}
