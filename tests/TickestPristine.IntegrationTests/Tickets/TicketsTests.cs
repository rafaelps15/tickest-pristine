using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;

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

        TicketDetailDto? ticket = await HttpClient.GetFromJsonAsync<TicketDetailDto>($"tickets/{ticketId}");
        ticket!.Title.ShouldBe("Printer is broken");
        ticket.Status.ShouldBe(1); // Open
        ticket.SectorId.ShouldBe(sectorId);
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

        TicketDetailDto? ticket = await HttpClient.GetFromJsonAsync<TicketDetailDto>($"tickets/{ticketId}");
        ticket!.Description.ShouldBe("Updated description");
        ticket.Status.ShouldBe(2); // InProgress
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

        TicketDetailDto? ticket = await HttpClient.GetFromJsonAsync<TicketDetailDto>($"tickets/{ticketId}");
        ticket!.Status.ShouldBe(1); // Open
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

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"tickets/{ticketId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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

    [Fact]
    public async Task GetAll_Should_IgnoreAssignee_WhenUnassignedOnlyIsTrue()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid assigneeId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        Guid unassignedTicketId = await CreateTicketAsync(sectorId);
        HttpResponseMessage assignedResponse = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = "Printer is broken",
            description = "The printer on the 3rd floor is not working",
            priority = 2,
            sectorId,
            assignedToUserId = assigneeId
        });
        assignedResponse.EnsureSuccessStatusCode();

        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"tickets?sectorId={sectorId}&unassignedOnly=true&assignedToUserId={assigneeId}");

        // Assert
        response.EnsureSuccessStatusCode();
        PagedTicketsDto? page = await response.Content.ReadFromJsonAsync<PagedTicketsDto>();
        page!.Items.ShouldHaveSingleItem().Id.ShouldBe(unassignedTicketId);
    }

    [Fact]
    public async Task GetStatusCounts_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("tickets/stats/status");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStatusCounts_Should_ReturnForbidden_WhenCallerLacksManagePermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("tickets/stats/status");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetStatusCounts_Should_CountOtherUsersTickets_WhenCallerHasManagePermission()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();

        StatusCountsDto? before = await HttpClient.GetFromJsonAsync<StatusCountsDto>("tickets/stats/status");

        Authenticate(requesterTokens.AccessToken);
        await CreateTicketAsync(sectorId);

        await AuthenticateAsAdminAsync();

        // Act
        StatusCountsDto? after = await HttpClient.GetFromJsonAsync<StatusCountsDto>("tickets/stats/status");

        // Assert
        after!.TotalCount.ShouldBe(before!.TotalCount + 1);
        after.OpenCount.ShouldBe(before.OpenCount + 1);
    }

    [Fact]
    public async Task GetVolume_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("tickets/stats/volume");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetVolume_Should_ReturnForbidden_WhenCallerLacksManagePermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("tickets/stats/volume");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetVolume_Should_CountNewTicketInCurrentDay_WhenGroupedByDay()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();

        List<VolumePointDto>? before = await HttpClient.GetFromJsonAsync<List<VolumePointDto>>("tickets/stats/volume?groupBy=Day");

        Authenticate(requesterTokens.AccessToken);
        await CreateTicketAsync(sectorId);

        await AuthenticateAsAdminAsync();

        // Act
        List<VolumePointDto>? after = await HttpClient.GetFromJsonAsync<List<VolumePointDto>>("tickets/stats/volume?groupBy=Day");

        // Assert
        after!.Count.ShouldBe(30);
        after[^1].Count.ShouldBe(before![^1].Count + 1);
    }

    [Fact]
    public async Task GetVolume_Should_CountTicketInLocalDay_WhenOpenedLateAtNight()
    {
        // Arrange
        (Guid requesterId, Guid sectorId) = await CreateRequesterAndSectorAsync();
        DateOnly yesterday = BrasiliaToday().AddDays(-1);
        Dictionary<DateOnly, int> before = await GetVolumeAsync("Day");

        // 22:00 em Brasília já é o dia seguinte em UTC.
        await InsertTicketAsync(requesterId, sectorId, BrasiliaToUtc(yesterday, 22));

        // Act
        Dictionary<DateOnly, int> after = await GetVolumeAsync("Day");

        // Assert
        after[yesterday].ShouldBe(before[yesterday] + 1);
        after[BrasiliaToday()].ShouldBe(before[BrasiliaToday()]);
    }

    [Fact]
    public async Task GetVolume_Should_CountTicketInPreviousWeek_WhenOpenedOnSundayNight()
    {
        // Arrange
        (Guid requesterId, Guid sectorId) = await CreateRequesterAndSectorAsync();
        DateOnly today = BrasiliaToday();
        DateOnly currentMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        DateOnly previousMonday = currentMonday.AddDays(-7);
        Dictionary<DateOnly, int> before = await GetVolumeAsync("Week");

        await InsertTicketAsync(requesterId, sectorId, BrasiliaToUtc(currentMonday.AddDays(-1), 23));

        // Act
        Dictionary<DateOnly, int> after = await GetVolumeAsync("Week");

        // Assert
        after[previousMonday].ShouldBe(before[previousMonday] + 1);
        after[currentMonday].ShouldBe(before[currentMonday]);
    }

    [Fact]
    public async Task GetVolume_Should_Return12MonthsAndCountTicketInPreviousMonth_WhenOpenedOnLastNightOfMonth()
    {
        // Arrange
        (Guid requesterId, Guid sectorId) = await CreateRequesterAndSectorAsync();
        DateOnly today = BrasiliaToday();
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        DateOnly previousMonth = currentMonth.AddMonths(-1);
        Dictionary<DateOnly, int> before = await GetVolumeAsync("Month");

        await InsertTicketAsync(requesterId, sectorId, BrasiliaToUtc(currentMonth.AddDays(-1), 23));

        // Act
        Dictionary<DateOnly, int> after = await GetVolumeAsync("Month");

        // Assert
        after.Count.ShouldBe(12);
        after.Keys.Min().ShouldBe(currentMonth.AddMonths(-11));
        after[previousMonth].ShouldBe(before[previousMonth] + 1);
        after[currentMonth].ShouldBe(before[currentMonth]);
    }

    [Fact]
    public async Task GetVolume_Should_NotCountTicket_WhenTicketIsDeleted()
    {
        // Arrange
        (Guid requesterId, Guid sectorId) = await CreateRequesterAndSectorAsync();
        Dictionary<DateOnly, int> before = await GetVolumeAsync("Day");

        await InsertTicketAsync(requesterId, sectorId, DateTime.UtcNow.AddMinutes(-1), deletedAtUtc: DateTime.UtcNow);

        // Act
        Dictionary<DateOnly, int> after = await GetVolumeAsync("Day");

        // Assert
        after[BrasiliaToday()].ShouldBe(before[BrasiliaToday()]);
    }

    [Fact]
    public async Task GetVolume_Should_Return12Weeks_WhenGroupedByWeek()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        List<VolumePointDto>? volume = await HttpClient.GetFromJsonAsync<List<VolumePointDto>>("tickets/stats/volume?groupBy=Week");

        // Assert
        volume!.Count.ShouldBe(12);
        volume.ShouldAllBe(p => p.PeriodStart.DayOfWeek == DayOfWeek.Monday);
    }

    [Fact]
    public async Task GetVolume_Should_ReturnBadRequest_WhenGroupByIsInvalid()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("tickets/stats/volume?groupBy=Year");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Should_ReturnForbidden_WhenOpeningOnBehalfOfAnotherUserWithoutManagePermission()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        Guid otherUserId = await RegisterUserAsync(UniqueEmail());
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = "Printer is broken",
            description = "The printer on the 3rd floor is not working",
            priority = 2,
            sectorId,
            requesterId = otherUserId
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.Unauthorized");
    }

    [Fact]
    public async Task Create_Should_ReturnNotFound_WhenResponsibleDoesNotExist()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = "Printer is broken",
            description = "The printer on the 3rd floor is not working",
            priority = 2,
            sectorId,
            assignedToUserId = Guid.NewGuid()
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.NotFound");
    }

    [Fact]
    public async Task Create_Should_ReturnBadRequest_WhenTitleIsEmpty()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = string.Empty,
            description = "The printer on the 3rd floor is not working",
            priority = 2,
            sectorId
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_Should_ReturnForbidden_WhenCallerIsNotOwnerAndLacksManagePermission()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        (_, AccessTokens outsiderTokens) = await RegisterAndLoginAsync();
        Authenticate(outsiderTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "Updated description",
            status = 2 // InProgress
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_Should_ReturnBadRequest_WhenTicketIsNotActive()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        HttpResponseMessage cancelResponse = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "The printer is working again",
            status = 5 // Canceled
        });
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "Updated description",
            status = 5 // Canceled
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Tickets.NotActive");
    }

    [Fact]
    public async Task Update_Should_ReturnBadRequest_WhenStatusTransitionIsNotAllowed()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act: de Aberto não se vai direto para Resolvido
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "Fixed the printer",
            status = 3 // Resolved
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Tickets.InvalidStatusTransition");
    }

    [Fact]
    public async Task Update_Should_RecordDescriptionChanged_WhenOwnerChangesOnlyDescription()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid ownerId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = ChangedDescription,
            status = 1 // Open
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        List<TicketHistory> newEntries = GetHistoryAfterCreation(ticketId);
        TicketHistory history = newEntries.ShouldHaveSingleItem();
        history.Action.ShouldBe(TicketHistoryAction.DescriptionChanged);
        history.OldValue.ShouldBe(OriginalDescription);
        history.NewValue.ShouldBe(ChangedDescription);
        history.ChangedByUserId.ShouldBe(ownerId);
    }

    [Fact]
    public async Task Update_Should_RecordStatusAndDescriptionChanges_WhenManagerChangesBoth()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = ChangedDescription,
            status = 2 // InProgress
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        List<TicketHistory> newEntries = GetHistoryAfterCreation(ticketId);
        newEntries.Count.ShouldBe(2);

        TicketHistory statusChanged = newEntries.Single(h => h.Action == TicketHistoryAction.StatusChanged);
        statusChanged.OldValue.ShouldBe(nameof(TicketStatus.Open));
        statusChanged.NewValue.ShouldBe(nameof(TicketStatus.InProgress));

        TicketHistory descriptionChanged = newEntries.Single(h => h.Action == TicketHistoryAction.DescriptionChanged);
        descriptionChanged.OldValue.ShouldBe(OriginalDescription);
        descriptionChanged.NewValue.ShouldBe(ChangedDescription);
    }

    [Fact]
    public async Task Update_Should_RecordOnlyStatusChanged_WhenDescriptionIsUnchanged()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = OriginalDescription,
            status = 2 // InProgress
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        GetHistoryAfterCreation(ticketId).ShouldHaveSingleItem().Action.ShouldBe(TicketHistoryAction.StatusChanged);
    }

    [Fact]
    public async Task Update_Should_NotRecordHistory_WhenNothingChanges()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = OriginalDescription,
            status = 1 // Open
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        GetHistoryAfterCreation(ticketId).ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_Should_ShowDescriptionChangeInHistory()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        HttpResponseMessage updateResponse = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = ChangedDescription,
            status = 1 // Open
        });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{ticketId}/history");

        // Assert
        response.EnsureSuccessStatusCode();
        List<TicketHistoryDto>? history = (await response.Content.ReadFromJsonAsync<CursorPageDto<TicketHistoryDto>>())?.Items;
        history.ShouldNotBeNull();
        history.ShouldContain(h => h.Action == 12 && h.Description == "Descrição alterada");
    }

    [Fact]
    public async Task Update_Should_NotRecordHistory_WhenTicketIsNotActive()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        HttpResponseMessage cancelResponse = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = OriginalDescription,
            status = 5 // Canceled
        });
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        int entriesBefore = GetHistoryAfterCreation(ticketId).Count;

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = ChangedDescription,
            status = 5 // Canceled
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Tickets.NotActive");
        GetHistoryAfterCreation(ticketId).Count.ShouldBe(entriesBefore);
    }

    [Fact]
    public async Task Update_Should_NotRecordHistory_WhenCallerLacksPermission()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        (_, AccessTokens outsiderTokens) = await RegisterAndLoginAsync();
        Authenticate(outsiderTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = ChangedDescription,
            status = 1 // Open
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        GetHistoryAfterCreation(ticketId).ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_Should_NotRecordHistory_WhenManagerSendsInvalidStatusTransition()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = ChangedDescription,
            status = 4 // Closed
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Tickets.InvalidStatusTransition");
        GetHistoryAfterCreation(ticketId).ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_Should_ReturnNotFound_WhenManagerUpdatesMissingTicket()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        var ticketId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = ChangedDescription,
            status = 2 // InProgress
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Tickets.NotFound");
        GetHistoryAfterCreation(ticketId).ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_Should_ReturnForbidden_WhenCallerIsNotOwnerAndLacksManagePermission()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens ownerTokens) = await RegisterAndLoginAsync();
        Authenticate(ownerTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        (_, AccessTokens outsiderTokens) = await RegisterAndLoginAsync();
        Authenticate(outsiderTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{ticketId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_Should_ReturnBadRequest_WhenTicketIsNotActive()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        HttpResponseMessage cancelResponse = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "The printer is working again",
            status = 5 // Canceled
        });
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{ticketId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Tickets.NotActive");
    }

    [Fact]
    public async Task Reopen_Should_ReturnConflict_WhenTicketIsAlreadyActive()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{ticketId}/reopen", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Tickets.AlreadyActive");
    }

    [Fact]
    public async Task GetById_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Close_Should_ReturnNoContent_WhenTicketIsResolvedAndCallerHasManagePermission()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        await ResolveTicketAsync(ticketId);

        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{ticketId}/close", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TicketDetailDto? ticket = await HttpClient.GetFromJsonAsync<TicketDetailDto>($"tickets/{ticketId}");
        ticket!.Status.ShouldBe(4); // Closed
    }

    [Fact]
    public async Task Close_Should_ReturnBadRequest_WhenTicketIsNotResolved()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{ticketId}/close", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Close_Should_ReturnForbidden_WhenCallerLacksManagePermission()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        await ResolveTicketAsync(ticketId);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync($"tickets/{ticketId}/close", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Assign_Should_SetAssigneeAndRecordHistory_WhenAssigneeManagesTickets()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        Guid adminId = GetUserId(adminTokens);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}/assignee", new
        {
            assignedToUserId = adminId
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TicketDetailDto? ticket = await HttpClient.GetFromJsonAsync<TicketDetailDto>($"tickets/{ticketId}");
        ticket!.AssignedToUserId.ShouldBe(adminId);

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        TicketHistory history = context.TicketHistories
            .Single(h => h.TicketId == ticketId && h.Action == TicketHistoryAction.Reassigned);
        history.OldValue.ShouldBeNull();
        history.NewValue.ShouldBe(adminId.ToString());
    }

    [Fact]
    public async Task Assign_Should_ReturnBadRequest_WhenAssigneeDoesNotManageTickets()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid requesterId, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}/assignee", new
        {
            assignedToUserId = requesterId
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Assign_Should_ReturnForbidden_WhenCallerLacksManagePermission()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid requesterId, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}/assignee", new
        {
            assignedToUserId = requesterId
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unassign_Should_RemoveAssigneeAndRecordHistory_WhenCallerManagesTickets()
    {
        // Arrange
        (Guid ticketId, Guid adminId) = await CreateTicketAssignedToAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{ticketId}/assignee");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TicketDetailDto? ticket = await HttpClient.GetFromJsonAsync<TicketDetailDto>($"tickets/{ticketId}");
        ticket!.AssignedToUserId.ShouldBeNull();

        TicketHistory history = GetUnassignedHistory(ticketId).ShouldHaveSingleItem();
        history.OldValue.ShouldBe(adminId.ToString());
        history.NewValue.ShouldBeNull();
        history.ChangedByUserId.ShouldBe(adminId);
    }

    [Fact]
    public async Task Unassign_Should_ReturnForbidden_WhenCallerLacksManagePermission()
    {
        // Arrange
        (Guid ticketId, Guid adminId) = await CreateTicketAssignedToAdminAsync();
        (_, AccessTokens outsiderTokens) = await RegisterAndLoginAsync();
        Authenticate(outsiderTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{ticketId}/assignee");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await AuthenticateAsAdminAsync();
        TicketDetailDto? ticket = await HttpClient.GetFromJsonAsync<TicketDetailDto>($"tickets/{ticketId}");
        ticket!.AssignedToUserId.ShouldBe(adminId);
    }

    [Fact]
    public async Task Unassign_Should_ReturnNotFound_WhenTicketDoesNotExist()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{Guid.NewGuid()}/assignee");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Tickets.NotFound");
    }

    [Fact]
    public async Task Unassign_Should_ReturnNotFound_WhenTicketWasDeleted()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAssignedToAdminAsync();
        HttpResponseMessage deleteResponse = await HttpClient.DeleteAsync($"tickets/{ticketId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{ticketId}/assignee");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Tickets.NotFound");
    }

    [Fact]
    public async Task Unassign_Should_ReturnBadRequest_WhenTicketIsNotActive()
    {
        // Arrange
        (Guid ticketId, Guid adminId) = await CreateTicketAssignedToAdminAsync();
        await ResolveTicketAsync(ticketId);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{ticketId}/assignee");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Tickets.NotActive");

        TicketDetailDto? ticket = await HttpClient.GetFromJsonAsync<TicketDetailDto>($"tickets/{ticketId}");
        ticket!.AssignedToUserId.ShouldBe(adminId);
        GetUnassignedHistory(ticketId).ShouldBeEmpty();
    }

    [Fact]
    public async Task Unassign_Should_ReturnBadRequest_WhenTicketIsNotActiveAndHasNoAssignee()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        await ResolveTicketAsync(ticketId);
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{ticketId}/assignee");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Tickets.NotActive");
        GetUnassignedHistory(ticketId).ShouldBeEmpty();
    }

    [Fact]
    public async Task Unassign_Should_ReturnNoContentWithoutHistory_WhenTicketHasNoAssignee()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{ticketId}/assignee");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        GetUnassignedHistory(ticketId).ShouldBeEmpty();
    }

    [Fact]
    public async Task Unassign_Should_KeepStatus_WhenTicketIsInProgress()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAssignedToAdminAsync();
        HttpResponseMessage inProgressResponse = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "Looking into the printer",
            status = 2 // InProgress
        });
        inProgressResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{ticketId}/assignee");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        TicketDetailDto? ticket = await HttpClient.GetFromJsonAsync<TicketDetailDto>($"tickets/{ticketId}");
        ticket!.Status.ShouldBe(2); // InProgress
        ticket.AssignedToUserId.ShouldBeNull();
    }

    [Fact]
    public async Task Unassign_Should_ShowRemovalInHistory()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAssignedToAdminAsync();
        HttpResponseMessage unassignResponse = await HttpClient.DeleteAsync($"tickets/{ticketId}/assignee");
        unassignResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{ticketId}/history");

        // Assert
        response.EnsureSuccessStatusCode();
        List<TicketHistoryDto>? history = (await response.Content.ReadFromJsonAsync<CursorPageDto<TicketHistoryDto>>())?.Items;
        history.ShouldNotBeNull();
        history.ShouldContain(h =>
            h.Action == (int)TicketHistoryAction.Unassigned &&
            h.Description == "Responsável removido");
    }

    [Fact]
    public async Task Unassign_Should_RevokeParticipantAccess_WhenFormerAssigneeIsNotRequesterNorManager()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid assigneeId, AccessTokens assigneeTokens) = await RegisterAndLoginAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        HttpResponseMessage createResponse = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = "Printer is broken",
            description = "The printer on the 3rd floor is not working",
            priority = 2,
            sectorId,
            assignedToUserId = assigneeId
        });
        createResponse.EnsureSuccessStatusCode();
        Guid ticketId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        Authenticate(assigneeTokens.AccessToken);
        (await HttpClient.GetAsync($"tickets/{ticketId}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        await AuthenticateAsAdminAsync();
        HttpResponseMessage unassignResponse = await HttpClient.DeleteAsync($"tickets/{ticketId}/assignee");

        // Act
        Authenticate(assigneeTokens.AccessToken);
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{ticketId}");

        // Assert
        unassignResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unassign_Should_NotRecordHistoryAgain_WhenAssigneeWasAlreadyRemoved()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAssignedToAdminAsync();
        HttpResponseMessage firstResponse = await HttpClient.DeleteAsync($"tickets/{ticketId}/assignee");
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{ticketId}/assignee");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        GetUnassignedHistory(ticketId).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Unassign_Should_ReturnValidationError_WhenTicketIdIsEmpty()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{Guid.Empty}/assignee");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Validation.General");
    }

    [Fact]
    public async Task Unassign_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        (Guid ticketId, Guid adminId) = await CreateTicketAssignedToAdminAsync();
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"tickets/{ticketId}/assignee");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await AuthenticateAsAdminAsync();
        TicketDetailDto? ticket = await HttpClient.GetFromJsonAsync<TicketDetailDto>($"tickets/{ticketId}");
        ticket!.AssignedToUserId.ShouldBe(adminId);
    }

    /// <summary>
    /// Abre um chamado como um solicitante novo e o atribui ao administrador, que fica com o token configurado.
    /// </summary>
    private async Task<(Guid TicketId, Guid AdminId)> CreateTicketAssignedToAdminAsync()
    {
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        Guid adminId = GetUserId(adminTokens);

        HttpResponseMessage assignResponse = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}/assignee", new
        {
            assignedToUserId = adminId
        });
        assignResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        return (ticketId, adminId);
    }

    private const string OriginalDescription = "The printer on the 3rd floor is not working";

    private const string ChangedDescription = "The printer on the 3rd floor is not working after the toner change";

    /// <summary>
    /// Registros do histórico do chamado, menos o da abertura (que todo chamado criado pela API tem).
    /// </summary>
    private List<TicketHistory> GetHistoryAfterCreation(Guid ticketId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return context.TicketHistories
            .Where(h => h.TicketId == ticketId && h.Action != TicketHistoryAction.Created)
            .ToList();
    }

    private List<TicketHistory> GetUnassignedHistory(Guid ticketId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return context.TicketHistories
            .Where(h => h.TicketId == ticketId && h.Action == TicketHistoryAction.Unassigned)
            .ToList();
    }

    /// <summary>
    /// Leva o chamado de Aberto a Resolvido pela API, com o token já configurado (dono ou quem gerencia chamados).
    /// </summary>
    private async Task ResolveTicketAsync(Guid ticketId)
    {
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
    }

    private static readonly TimeZoneInfo BrasiliaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private static DateOnly BrasiliaToday() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BrasiliaTimeZone));

    private static DateTime BrasiliaToUtc(DateOnly date, int hour) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(hour, 0)), BrasiliaTimeZone);

    private async Task<(Guid RequesterId, Guid SectorId)> CreateRequesterAndSectorAsync()
    {
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid requesterId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        return (requesterId, sectorId);
    }

    private async Task<Dictionary<DateOnly, int>> GetVolumeAsync(string groupBy)
    {
        await AuthenticateAsAdminAsync();

        List<VolumePointDto>? volume = await HttpClient.GetFromJsonAsync<List<VolumePointDto>>($"tickets/stats/volume?groupBy={groupBy}");

        return volume!.ToDictionary(p => p.PeriodStart, p => p.Count);
    }

    /// <summary>
    /// Grava o chamado direto no banco: pela API a data de abertura é sempre agora, e os testes de volume precisam de
    /// aberturas em horários e dias escolhidos.
    /// </summary>
    private async Task InsertTicketAsync(Guid requesterId, Guid sectorId, DateTime createdAtUtc, DateTime? deletedAtUtc = null)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        context.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "The printer on the 3rd floor is not working",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            CreatedByUserId = requesterId,
            SectorId = sectorId,
            CreatedAtUtc = createdAtUtc,
            DueAtUtc = createdAtUtc.AddHours(8),
            SlaWarningAtUtc = createdAtUtc.AddHours(6),
            DeletedAtUtc = deletedAtUtc
        });

        await context.SaveChangesAsync();
    }

    private sealed record TicketDto(Guid Id, string Title, string Description, int Priority, int Status);

    private sealed record PagedOwnTicketsDto(List<TicketDto> Items, int TotalCount);

    private sealed record TicketDetailDto(
        Guid Id,
        string Title,
        string Description,
        Guid OpenedByUserId,
        Guid? AssignedToUserId,
        Guid SectorId,
        int Status,
        int SlaStatus);

    private sealed record ProblemDto(int Status, string Title, string Detail);

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

    private sealed record StatusCountsDto(int TotalCount, int OpenCount, int InProgressCount, int ResolvedCount);

    private sealed record VolumePointDto(DateOnly PeriodStart, int Count);

    private sealed record TicketHistoryDto(Guid Id, Guid TicketId, Guid? ChangedByUserId, int Action, string Description);
}
