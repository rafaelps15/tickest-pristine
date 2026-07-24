using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace TickestPristine.IntegrationTests.TicketAttachments;

public sealed class TicketAttachmentsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string AdminEmail = "admin@tickestpristine.dev";
    private const string AdminPassword = "ChangeMe123!";

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

    // Ownership of fileContent is transferred to the returned MultipartFormDataContent, which disposes
    // it - callers are responsible for disposing the returned content itself.
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
        // Act
        using MultipartFormDataContent content = BuildFileContent();
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
    public async Task Upload_Should_ReturnProblem_WhenContentTypeIsNotAllowed()
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
    public async Task Upload_Then_GetByTicket_Should_ReturnUploadedAttachment_WhenCallerIsTicketCreator()
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
        List<TicketAttachmentDto>? attachments = await getResponse.Content.ReadFromJsonAsync<List<TicketAttachmentDto>>();
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
        List<TicketAttachmentDto>? attachments = await getResponse.Content.ReadFromJsonAsync<List<TicketAttachmentDto>>();
        attachments!.ShouldNotContain(a => a.Id == attachmentId);
    }

    private sealed record TicketAttachmentDto(
        Guid Id,
        Guid TicketId,
        Guid UploadedByUserId,
        string FileName,
        string ContentType,
        long FileSizeBytes);
}
