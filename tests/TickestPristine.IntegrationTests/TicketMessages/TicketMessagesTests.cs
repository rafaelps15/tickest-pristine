using System.Net;
using System.Net.Http.Json;

namespace TickestPristine.IntegrationTests.TicketMessages;

public sealed class TicketMessagesTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
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

    [Fact]
    public async Task Post_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        var request = new { content = "Hello" };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync($"tickets/{Guid.NewGuid()}/messages", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"tickets/{Guid.NewGuid()}/messages",
            new { content = "Hello" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_Should_ReturnMessageId_WhenCallerIsTicketCreator()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"tickets/{ticketId}/messages",
            new { content = "Any update on this?" });

        // Assert
        response.EnsureSuccessStatusCode();
        Guid messageId = await response.Content.ReadFromJsonAsync<Guid>();
        messageId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetAll_Should_ReturnPostedMessage_WhenCallerIsParticipant()
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
        Guid messageId = await postResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{ticketId}/messages");

        // Assert
        response.EnsureSuccessStatusCode();
        List<TicketMessageDto>? messages = (await response.Content.ReadFromJsonAsync<CursorPageDto<TicketMessageDto>>())?.Items;
        messages!.ShouldContain(m => m.Id == messageId && m.Content == "Any update on this?");
    }

    [Fact]
    public async Task GetAll_Should_WalkAllMessagesNewestFirst_WhenFollowingTheCursor()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        var postedIds = new List<Guid>();
        foreach (string content in new[] { "First", "Second", "Third" })
        {
            HttpResponseMessage postResponse = await HttpClient.PostAsJsonAsync($"tickets/{ticketId}/messages", new { content });
            postResponse.EnsureSuccessStatusCode();
            postedIds.Add(await postResponse.Content.ReadFromJsonAsync<Guid>());
        }

        // Act
        CursorPageDto<TicketMessageDto>? firstPage = await HttpClient.GetFromJsonAsync<CursorPageDto<TicketMessageDto>>(
            $"tickets/{ticketId}/messages?pageSize=2");

        string cursorDate = Uri.EscapeDataString(firstPage!.NextCursorDate!.Value.ToString("O"));
        CursorPageDto<TicketMessageDto>? secondPage = await HttpClient.GetFromJsonAsync<CursorPageDto<TicketMessageDto>>(
            $"tickets/{ticketId}/messages?pageSize=2&cursorDate={cursorDate}&cursorId={firstPage.NextCursorId}");

        // Assert
        firstPage.Items.Select(m => m.Content).ShouldBe(["Third", "Second"]);
        firstPage.HasNextPage.ShouldBeTrue();

        secondPage!.Items.Select(m => m.Content).ShouldBe(["First"]);
        secondPage.HasNextPage.ShouldBeFalse();

        firstPage.Items.Concat(secondPage.Items).Select(m => m.Id).ShouldBe(postedIds, ignoreOrder: true);
    }

    [Fact]
    public async Task Edit_Should_ReturnNoContent_WhenCallerIsAuthor()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        HttpResponseMessage postResponse = await HttpClient.PostAsJsonAsync(
            $"tickets/{ticketId}/messages",
            new { content = "Original content" });
        postResponse.EnsureSuccessStatusCode();
        Guid messageId = await postResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"ticket-messages/{messageId}",
            new { content = "Edited content" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"tickets/{ticketId}/messages");
        List<TicketMessageDto>? messages = (await getResponse.Content.ReadFromJsonAsync<CursorPageDto<TicketMessageDto>>())?.Items;
        messages!.ShouldContain(m => m.Id == messageId && m.Content == "Edited content");
    }

    [Fact]
    public async Task Delete_Should_ReturnNoContent_WhenCallerIsAuthor()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        HttpResponseMessage postResponse = await HttpClient.PostAsJsonAsync(
            $"tickets/{ticketId}/messages",
            new { content = "To be deleted" });
        postResponse.EnsureSuccessStatusCode();
        Guid messageId = await postResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"ticket-messages/{messageId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"tickets/{ticketId}/messages");
        List<TicketMessageDto>? messages = (await getResponse.Content.ReadFromJsonAsync<CursorPageDto<TicketMessageDto>>())?.Items;
        messages!.ShouldNotContain(m => m.Id == messageId);
    }

    private sealed record TicketMessageDto(Guid Id, Guid TicketId, Guid AuthorUserId, string Content);
}
