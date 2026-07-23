using System.Net;
using System.Net.Http.Json;

namespace TickestPristine.IntegrationTests.Tickets;

public sealed class TicketsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
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
    public async Task Create_Should_ReturnTicketId_WhenSectorExistsAndCallerHasTicketsCreatePermission()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Assert
        ticketId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Create_Should_ReturnNotFound_WhenSectorDoesNotExist()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = "Printer is broken",
            description = "The printer on the 3rd floor is not working",
            priority = 2,
            sectorId = Guid.NewGuid()
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Should_ReturnForbidden_WhenCallerHasNoRoles()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();

        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens userTokens = await LoginAsync(email);

        await AuthenticateAsAdminAsync();
        HttpResponseMessage stripRolesResponse = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/roles",
            new { roleIds = Array.Empty<Guid>() });
        stripRolesResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Authenticate(userTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = "Printer is broken",
            description = "The printer on the 3rd floor is not working",
            priority = 2,
            sectorId
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_Should_ReturnNoContent_WhenOwnerUpdatesOwnTicket()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "Updated description",
            status = 2 // InProgress
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetByUser_Should_ReturnActiveTickets_WhenCallerRequestsOwnTickets()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/users/{userId}");

        // Assert
        response.EnsureSuccessStatusCode();
        List<TicketDto>? tickets = await response.Content.ReadFromJsonAsync<List<TicketDto>>();
        tickets!.ShouldContain(t => t.Id == ticketId);
    }

    [Fact]
    public async Task Reopen_Should_ReturnNoContent_WhenCallerIsAdminAndTicketIsInactive()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        HttpResponseMessage inProgressResponse = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "Looking into the printer",
            status = 2 // InProgress
        });
        inProgressResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage resolveResponse = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "Fixed the printer",
            status = 3 // Resolved
        });
        resolveResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{ticketId}/reopen", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_Should_ReturnNoContent_WhenCallerIsAdmin()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{ticketId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private sealed record TicketDto(Guid Id, string Title, string Description, int Priority, int Status);
}
