using System.Net;
using System.Net.Http.Json;

namespace TickestPristine.IntegrationTests.Tickets;

public sealed class TicketsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
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
    public async Task GetOwn_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("tickets/mine");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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
            new { roleNames = Array.Empty<string>() });
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
    public async Task GetOwn_Should_ReturnOpenTicket_WhenCallerHasOpenedIt()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("tickets/mine");

        // Assert
        response.EnsureSuccessStatusCode();
        List<TicketDto>? tickets = (await response.Content.ReadFromJsonAsync<PagedOwnTicketsDto>())?.Items;
        tickets!.ShouldContain(t => t.Id == ticketId);
    }

    [Fact]
    public async Task GetOwn_Should_ReturnResolvedTicket_WhenCallerHasOpenedIt()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
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

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("tickets/mine");

        // Assert
        response.EnsureSuccessStatusCode();
        List<TicketDto>? tickets = (await response.Content.ReadFromJsonAsync<PagedOwnTicketsDto>())?.Items;
        tickets!.ShouldContain(t => t.Id == ticketId && t.Status == 3);
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

    [Fact]
    public async Task GetAll_Should_ReturnForbidden_WhenCallerLacksManagePermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("tickets");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAll_Should_ReturnTicketsWithRequesterAndSla_WhenCallerIsAdmin()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid requesterId, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets?sectorId={sectorId}");

        // Assert
        response.EnsureSuccessStatusCode();
        PagedTicketsDto? page = await response.Content.ReadFromJsonAsync<PagedTicketsDto>();
        TicketSummaryDto ticket = page!.Items.ShouldHaveSingleItem();
        ticket.Id.ShouldBe(ticketId);
        ticket.RequesterId.ShouldBe(requesterId);
        ticket.RequesterName.ShouldNotBeNullOrWhiteSpace();
        ticket.DueAtUtc.ShouldBe(ticket.CreatedAtUtc.AddHours(24), TimeSpan.FromSeconds(1));
        ticket.SlaStatus.ShouldBe(1); // OnTime
    }

    [Fact]
    public async Task GetAll_Should_FilterByStatusAndSlaStatus_WhenTicketWasResolvedWithinDeadline()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        await AuthenticateAsAdminAsync();

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

        // Act
        HttpResponseMessage metResponse = await HttpClient.GetAsync($"tickets?sectorId={sectorId}&status=3&status=4&slaStatus=4");
        HttpResponseMessage missedResponse = await HttpClient.GetAsync($"tickets?sectorId={sectorId}&status=3&slaStatus=5");

        // Assert
        metResponse.EnsureSuccessStatusCode();
        PagedTicketsDto? metPage = await metResponse.Content.ReadFromJsonAsync<PagedTicketsDto>();
        TicketSummaryDto ticket = metPage!.Items.ShouldHaveSingleItem();
        ticket.Id.ShouldBe(ticketId);
        ticket.Status.ShouldBe(3); // Resolved
        ticket.SlaStatus.ShouldBe(4); // Met

        missedResponse.EnsureSuccessStatusCode();
        PagedTicketsDto? missedPage = await missedResponse.Content.ReadFromJsonAsync<PagedTicketsDto>();
        missedPage!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetById_Should_ReturnTicket_WhenCallerIsRequester()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid requesterId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{ticketId}");

        // Assert
        response.EnsureSuccessStatusCode();
        TicketDetailDto? ticket = await response.Content.ReadFromJsonAsync<TicketDetailDto>();
        ticket!.Id.ShouldBe(ticketId);
        ticket.OpenedByUserId.ShouldBe(requesterId);
        ticket.SectorId.ShouldBe(sectorId);
        ticket.SlaStatus.ShouldBe(1); // OnTime
    }

    [Fact]
    public async Task GetById_Should_ReturnForbidden_WhenCallerIsNotParticipant()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        (_, AccessTokens outsiderTokens) = await RegisterAndLoginAsync();
        Authenticate(outsiderTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{ticketId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetOwnSummary_Should_CountOwnTicketsByPriorityAndStatus()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        await CreateTicketAsync(sectorId);
        await CreateTicketAsync(sectorId);

        // Act
        List<TicketCountDto>? counts = await HttpClient.GetFromJsonAsync<List<TicketCountDto>>("tickets/mine/summary");

        // Assert
        TicketCountDto count = counts!.ShouldHaveSingleItem();
        count.Priority.ShouldBe(2); // Medium
        count.Status.ShouldBe(1); // Open
        count.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetOwn_Should_NotReturnOtherUsersTickets()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid otherUsersTicketId = await CreateTicketAsync(sectorId);

        (_, AccessTokens callerTokens) = await RegisterAndLoginAsync();
        Authenticate(callerTokens.AccessToken);

        // Act
        PagedOwnTicketsDto? page = await HttpClient.GetFromJsonAsync<PagedOwnTicketsDto>("tickets/mine");

        // Assert
        page!.Items.ShouldNotContain(t => t.Id == otherUsersTicketId);
        page.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetOwnSummary_Should_NotCountOtherUsersTickets()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        await CreateTicketAsync(sectorId);

        (_, AccessTokens callerTokens) = await RegisterAndLoginAsync();
        Authenticate(callerTokens.AccessToken);

        // Act
        List<TicketCountDto>? counts = await HttpClient.GetFromJsonAsync<List<TicketCountDto>>("tickets/mine/summary");

        // Assert
        counts!.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetOwn_Should_ReturnRequestedPage_WhenPageSizeIsGiven()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        await CreateTicketAsync(sectorId);
        await CreateTicketAsync(sectorId);
        await CreateTicketAsync(sectorId);

        // Act
        PagedOwnTicketsDto? page = await HttpClient.GetFromJsonAsync<PagedOwnTicketsDto>(
            "tickets/mine?page=2&pageSize=2");

        // Assert
        page!.Items.ShouldHaveSingleItem();
        page.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task GetSlaSummary_Should_ReturnForbidden_WhenCallerLacksManagePermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("tickets/sla-summary");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSlaSummary_Should_CountActiveTicketsOfSector_WhenCallerIsAdmin()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        await CreateTicketAsync(sectorId);
        await CreateTicketAsync(sectorId);

        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/sla-summary?sectorId={sectorId}");

        // Assert
        response.EnsureSuccessStatusCode();
        SlaSummaryDto? summary = await response.Content.ReadFromJsonAsync<SlaSummaryDto>();
        summary!.ActiveCount.ShouldBe(2);
        summary.OnTimeCount.ShouldBe(2);
        summary.AtRiskCount.ShouldBe(0);
        summary.BreachedCount.ShouldBe(0);
    }

    [Fact]
    public async Task Update_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{Guid.NewGuid()}", new
        {
            description = "Updated description",
            status = 2 // InProgress
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reopen_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{Guid.NewGuid()}/reopen", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reopen_Should_ReturnForbidden_WhenCallerIsNotOwnerAndTicketIsActive()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        (_, AccessTokens outsiderTokens) = await RegisterAndLoginAsync();
        Authenticate(outsiderTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{ticketId}/reopen", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAll_Should_ReturnEmployeeTicketsInAllStatusesNewestFirst_WhenAdminFiltersByRequester()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid employeeId, AccessTokens employeeTokens) = await RegisterAndLoginAsync();
        Authenticate(employeeTokens.AccessToken);
        Guid resolvedTicketId = await CreateTicketAsync(sectorId);
        Guid openTicketId = await CreateTicketAsync(sectorId);

        (_, AccessTokens otherTokens) = await RegisterAndLoginAsync();
        Authenticate(otherTokens.AccessToken);
        await CreateTicketAsync(sectorId);

        await AuthenticateAsAdminAsync();
        (await HttpClient.PutAsJsonAsync($"tickets/{resolvedTicketId}", new { description = "Looking into the printer", status = 2 }))
            .EnsureSuccessStatusCode();
        (await HttpClient.PutAsJsonAsync($"tickets/{resolvedTicketId}", new { description = "Fixed the printer", status = 3 }))
            .EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets?requesterId={employeeId}");

        // Assert
        response.EnsureSuccessStatusCode();
        PagedTicketsDto? page = await response.Content.ReadFromJsonAsync<PagedTicketsDto>();
        page!.Items.Select(t => t.Id).ShouldBe([openTicketId, resolvedTicketId]);
        page.Items.Select(t => t.Status).ShouldBe([1, 3]); // Open, Resolved
    }

    private sealed record TicketDto(Guid Id, string Title, string Description, int Priority, int Status);

    private sealed record PagedOwnTicketsDto(List<TicketDto> Items, int TotalCount);

    private sealed record TicketDetailDto(Guid Id, Guid OpenedByUserId, Guid SectorId, int Status, int SlaStatus);

    private sealed record TicketCountDto(int Priority, int Status, int Count);

    private sealed record PagedTicketsDto(List<TicketSummaryDto> Items, int TotalCount);

    private sealed record TicketSummaryDto(
        Guid Id,
        Guid RequesterId,
        string RequesterName,
        DateTime CreatedAtUtc,
        DateTime DueAtUtc,
        int Status,
        int SlaStatus);

    private sealed record SlaSummaryDto(int ActiveCount, int OnTimeCount, int AtRiskCount, int BreachedCount);
}
