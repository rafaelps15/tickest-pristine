using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace TickestPristine.IntegrationTests.TicketAttachments;

public sealed class TicketAttachmentsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
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
        string fileText = "test file content")
    {
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(fileText));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return new MultipartFormDataContent { { fileContent, "file", fileName } };
    }
#pragma warning restore CA2000

    private async Task<Guid> UploadAttachmentAsync(
        Guid ticketId,
        string fileName = "report.pdf",
        string contentType = "application/pdf",
        string fileText = "test file content")
    {
        using MultipartFormDataContent content = BuildFileContent(fileName, contentType, fileText);
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{ticketId}/attachments", content);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
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
        Guid attachmentId = await UploadAttachmentAsync(ticketId, fileText: "hello from the attachment");

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
    public async Task Upload_Should_RejectBodyBeforeValidation_WhenBodyExceedsLimit()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act: 12 MB passam do limite de 11 MB do corpo da requisição
        using MultipartFormDataContent content = BuildFileContent(fileText: new string('a', 12 * 1024 * 1024));
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{ticketId}/attachments", content);

        // Assert
        // O TestServer não aplica o limite do Kestrel (que responderia 413); aqui quem recusa é o limite do multipart, ao
        // ler o formulário. Nos dois casos o corpo para antes do validator, que responderia com o erro de validação.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Requisição inválida");
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

    private sealed record ProblemDto(int Status, string Title, string Detail);

    private sealed record TicketAttachmentDto(
        Guid Id,
        Guid TicketId,
        Guid UploadedByUserId,
        string FileName,
        string ContentType,
        long FileSizeBytes);
}
