using System.Net;
using System.Net.Http.Json;

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
    public async Task GetByTicket_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{Guid.NewGuid()}/history");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetByTicket_Should_ReturnNotFound_WhenTicketDoesNotExist()
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
    public async Task GetByTicket_Should_ContainCreatedEntry_WhenTicketWasJustOpened()
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
    public async Task GetByTicket_Should_ContainMessageAddedEntry_WhenMessageWasPosted()
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
    public async Task GetByTicket_Should_ContainStatusChangedEntry_WhenTicketStatusWasUpdated()
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
    public async Task GetByTicket_Should_ContainReopenedEntry_WhenTicketWasReopened()
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

    private sealed record TicketHistoryDto(Guid Id, Guid TicketId, Guid? ChangedByUserId, int Action, string Description);
}
