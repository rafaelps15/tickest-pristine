using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.TicketHistories;

public sealed class TicketHistoriesTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
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

    [Fact]
    public async Task GetAll_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        var ticketId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{ticketId}/history");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{Guid.NewGuid()}/history");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_Should_ContainCreatedEntry_WhenTicketWasJustOpened()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{ticketId}/history");

        // Assert
        response.EnsureSuccessStatusCode();
        List<TicketHistoryDto>? history = await response.Content.ReadFromJsonAsync<List<TicketHistoryDto>>();
        history!.ShouldContain(h => h.TicketId == ticketId && h.Action == 1 /* Created */);
    }

    [Fact]
    public async Task GetAll_Should_ContainMessageAddedEntry_WhenMessageWasPosted()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        HttpResponseMessage postResponse = await HttpClient.PostAsJsonAsync(
            $"tickets/{ticketId}/messages",
            new { content = "Any update on this?" });
        postResponse.EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{ticketId}/history");

        // Assert
        response.EnsureSuccessStatusCode();
        List<TicketHistoryDto>? history = await response.Content.ReadFromJsonAsync<List<TicketHistoryDto>>();
        history!.ShouldContain(h => h.TicketId == ticketId && h.Action == 6 /* MessageAdded */);
    }

    [Fact]
    public async Task GetAll_Should_ContainStatusChangedEntry_WhenTicketStatusWasUpdated()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        HttpResponseMessage updateResponse = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "The printer on the 3rd floor is not working",
            status = 2 /* InProgress */
        });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{ticketId}/history");

        // Assert
        response.EnsureSuccessStatusCode();
        List<TicketHistoryDto>? history = await response.Content.ReadFromJsonAsync<List<TicketHistoryDto>>();
        history!.ShouldContain(h => h.TicketId == ticketId && h.Action == 2 /* StatusChanged */);
    }

    [Fact]
    public async Task GetAll_Should_ContainReopenedEntry_WhenTicketWasReopened()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        HttpResponseMessage cancelResponse = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "The printer on the 3rd floor is not working",
            status = 5 /* Canceled */
        });
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage reopenResponse = await HttpClient.PostAsync($"tickets/{ticketId}/reopen", content: null);
        reopenResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{ticketId}/history");

        // Assert
        response.EnsureSuccessStatusCode();
        List<TicketHistoryDto>? history = await response.Content.ReadFromJsonAsync<List<TicketHistoryDto>>();
        history!.ShouldContain(h => h.TicketId == ticketId && h.Action == 4 /* Reopened */);
    }

    [Fact]
    public async Task GetAll_Should_ContainMessageEditedEntry_WhenMessageWasEdited()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        Guid messageId = await PostMessageAsync(ticketId);

        HttpResponseMessage editResponse = await HttpClient.PutAsJsonAsync(
            $"ticket-messages/{messageId}",
            new { content = "Any update on this? (edited)" });
        editResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        List<TicketHistoryDto> history = await GetHistoryAsync(ticketId);

        // Assert
        history.ShouldContain(h => h.Action == (int)TicketHistoryAction.MessageEdited);
    }

    [Fact]
    public async Task GetAll_Should_ContainMessageRemovedEntry_WhenMessageWasDeleted()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        Guid messageId = await PostMessageAsync(ticketId);

        HttpResponseMessage deleteResponse = await HttpClient.DeleteAsync($"ticket-messages/{messageId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        List<TicketHistoryDto> history = await GetHistoryAsync(ticketId);

        // Assert
        history.ShouldContain(h => h.Action == (int)TicketHistoryAction.MessageRemoved);
    }

    [Fact]
    public async Task GetAll_Should_ContainAttachmentAddedEntry_WhenAttachmentWasUploaded()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        await UploadAttachmentAsync(ticketId);

        // Act
        List<TicketHistoryDto> history = await GetHistoryAsync(ticketId);

        // Assert
        history.ShouldContain(h => h.Action == (int)TicketHistoryAction.AttachmentAdded);
    }

    [Fact]
    public async Task GetAll_Should_ContainAttachmentRemovedEntry_WhenAttachmentWasDeleted()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        Guid attachmentId = await UploadAttachmentAsync(ticketId);

        HttpResponseMessage deleteResponse = await HttpClient.DeleteAsync($"ticket-attachments/{attachmentId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        List<TicketHistoryDto> history = await GetHistoryAsync(ticketId);

        // Assert
        history.ShouldContain(h => h.Action == (int)TicketHistoryAction.AttachmentRemoved);
    }

    [Fact]
    public async Task Delete_Should_RecordDeletedEntry_WhenTicketWasDeleted()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        HttpResponseMessage deleteResponse = await HttpClient.DeleteAsync($"tickets/{ticketId}");

        // Assert: o chamado excluído some da API, então o histórico é conferido direto no banco.
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool deletedEntryExists = await context.TicketHistories.AnyAsync(h =>
            h.TicketId == ticketId &&
            h.Action == TicketHistoryAction.Deleted &&
            h.ChangedByUserId == userId);
        deletedEntryExists.ShouldBeTrue();
    }

    private async Task<Guid> PostMessageAsync(Guid ticketId)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"tickets/{ticketId}/messages",
            new { content = "Any update on this?" });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    // O conteúdo retornado fica responsável por liberar o arquivo.
#pragma warning disable CA2000
    private static MultipartFormDataContent BuildPdfContent()
    {
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("test file content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

        return new MultipartFormDataContent { { fileContent, "file", "report.pdf" } };
    }
#pragma warning restore CA2000

    private async Task<Guid> UploadAttachmentAsync(Guid ticketId)
    {
        using MultipartFormDataContent content = BuildPdfContent();

        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{ticketId}/attachments", content);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<List<TicketHistoryDto>> GetHistoryAsync(Guid ticketId)
    {
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{ticketId}/history");
        response.EnsureSuccessStatusCode();

        List<TicketHistoryDto>? history = await response.Content.ReadFromJsonAsync<List<TicketHistoryDto>>();

        return history!;
    }

    private sealed record TicketHistoryDto(Guid Id, Guid TicketId, Guid? ChangedByUserId, int Action, string Description);
}
