using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Audit;
using TickestPristine.Infrastructure.Database.Migrations;

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
        // Média: 1.200 minutos úteis no calendário "Expediente", contados da abertura.
        ticket.DueAtUtc.ShouldBe(
            DefaultSlaCalendar.Create().AddBusinessTime(ticket.CreatedAtUtc, TimeSpan.FromMinutes(1200)),
            TimeSpan.FromSeconds(1));
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
        TicketHistory history = await context.TicketHistories
            .SingleAsync(h => h.TicketId == ticketId && h.Action == TicketHistoryAction.Reassigned);
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
            DeletedAtUtc = deletedAtUtc,
            SlaPlan = TicketSla.LegacyPlan(TicketPriority.Medium)
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

    // Valores numéricos dos enums de SLA, como a API devolve.
    private const int FirstResponse = 1;
    private const int Resolution = 2;

    private const int Met = 4;
    private const int Stopped = 6;

    /// <summary>
    /// Chamado Urgente aberto por um Colaborador novo; devolve o chamado e o token do solicitante, já configurado.
    /// </summary>
    private async Task<(Guid TicketId, AccessTokens RequesterTokens)> CreateTicketAsRequesterAsync()
    {
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = "Printer is broken",
            description = "The printer on the 3rd floor is not working",
            priority = 4,
            sectorId
        });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<Guid>(), requesterTokens);
    }

    /// <summary>
    /// Usuário novo com a role Atendente (que tem <c>tickets:manage</c>), atribuída pelo administrador.
    /// </summary>
    private async Task<AccessTokens> CreateAgentAsync()
    {
        string email = UniqueEmail();
        Guid agentId = await RegisterUserAsync(email);

        await AuthenticateAsAdminAsync();
        (await HttpClient.PutAsJsonAsync($"users/{agentId}/roles", new { roleNames = new[] { RoleNames.Agent } }))
            .EnsureSuccessStatusCode();

        return await LoginAsync(email);
    }

    private async Task<SlaDto> GetSlaAsync(Guid ticketId)
    {
        SlaDto? sla = await HttpClient.GetFromJsonAsync<SlaDto>($"tickets/{ticketId}/sla");

        return sla!;
    }

    private async Task<HttpResponseMessage> PostMessageAsync(Guid ticketId, string content) =>
        await HttpClient.PostAsJsonAsync($"tickets/{ticketId}/messages", new { content });

    private async Task<HttpResponseMessage> SendMessageAsAsync(AccessTokens tokens, Guid ticketId, string content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"tickets/{ticketId}/messages")
        {
            Content = JsonContent.Create(new { content })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return await HttpClient.SendAsync(request);
    }

    private async Task UpdateStatusAsync(Guid ticketId, int status)
    {
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "The printer on the 3rd floor is not working",
            status
        });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private List<TicketSlaTimer> GetTimers(Guid ticketId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return [.. context.TicketSlaTimers.AsNoTracking().Where(t => t.TicketId == ticketId).OrderBy(t => t.Metric).ThenBy(t => t.Cycle)];
    }

    [Fact]
    public async Task GetByIdAndGetSla_Should_ReturnSameSlaBlock_WhenCallerIsParticipant()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();

        // Act
        HttpResponseMessage detailResponse = await HttpClient.GetAsync($"tickets/{ticketId}");
        HttpResponseMessage slaResponse = await HttpClient.GetAsync($"tickets/{ticketId}/sla");

        // Assert
        detailResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        slaResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        SlaTicketDetailDto? detail = await detailResponse.Content.ReadFromJsonAsync<SlaTicketDetailDto>();
        SlaDto? sla = await slaResponse.Content.ReadFromJsonAsync<SlaDto>();

        detail.ShouldNotBeNull();
        detail.Id.ShouldBe(ticketId);
        detail.Title.ShouldBe("Printer is broken");
        detail.SlaStatus.ShouldBe(1);
        detail.Sla.ShouldNotBeNull();
        sla.ShouldNotBeNull();

        detail.Sla.Plan.ShouldBe(sla.Plan);
        detail.Sla.Objectives.Select(o => (o.Metric, o.Cycle, o.TargetMinutes, o.StartedAtUtc, o.WarningAtUtc, o.DueAtUtc, o.IsCurrentCycle))
            .ShouldBe(sla.Objectives.Select(o => (o.Metric, o.Cycle, o.TargetMinutes, o.StartedAtUtc, o.WarningAtUtc, o.DueAtUtc, o.IsCurrentCycle)));
        detail.DueAtUtc.ShouldBe(sla.Objectives.Single(o => o.Metric == Resolution).DueAtUtc!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task GetByIdAndGetSla_Should_ReturnForbiddenUnauthorizedAndNotFound_WhenCallerCannotSeeTicket()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        (_, AccessTokens outsiderTokens) = await RegisterAndLoginAsync();

        foreach (string route in new[] { $"tickets/{ticketId}", $"tickets/{ticketId}/sla" })
        {
            // Act & Assert: quem não participa e não atende a fila.
            Authenticate(outsiderTokens.AccessToken);
            (await HttpClient.GetAsync(route)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

            // Sem autenticação.
            HttpClient.DefaultRequestHeaders.Authorization = null;
            (await HttpClient.GetAsync(route)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        Authenticate(outsiderTokens.AccessToken);
        (await HttpClient.GetAsync($"tickets/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await HttpClient.GetAsync($"tickets/{Guid.NewGuid()}/sla")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await HttpClient.GetAsync("tickets/not-a-guid/sla")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSla_Should_ReturnDefaultPlanAndAllObjectiveFields_WhenTicketIsNew()
    {
        // Arrange
        DateTime beforeUtc = DateTime.UtcNow;
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();

        // Act
        SlaDto sla = await GetSlaAsync(ticketId);

        // Assert
        sla.ServerNowUtc.ShouldBeInRange(beforeUtc, DateTime.UtcNow);
        sla.Plan.ShouldBe(new SlaPlanDto(null, "Padrão", 1, SlaCalendarIds.Default, "Expediente", false));

        sla.Objectives.Select(o => o.Metric).ShouldBe([FirstResponse, Resolution]);
        sla.Objectives.ShouldAllBe(o => o.Cycle == 1 && o.IsCurrentCycle && o.CompletedAtUtc == null && o.StoppedAtUtc == null);
        sla.Objectives.ShouldAllBe(o => o.StartedAtUtc != null && o.WarningAtUtc != null && o.DueAtUtc != null);
        sla.Objectives.ShouldAllBe(o => o.ConsumedMinutes != null && o.RemainingMinutes == o.TargetMinutes - o.ConsumedMinutes);
        sla.Objectives.Select(o => o.TargetMinutes).ShouldBe([30, 240]);

        BusinessCalendar calendar = DefaultSlaCalendar.Create();
        SlaObjectiveDto resolution = sla.Objectives.Single(o => o.Metric == Resolution);
        resolution.DueAtUtc!.Value.ShouldBe(calendar.AddBusinessTime(resolution.StartedAtUtc!.Value, TimeSpan.FromMinutes(240)), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task PostMessage_Should_CompleteFirstResponseOnlyForAgent_AndKeepItAfterEditAndDelete()
    {
        // Arrange
        (Guid ticketId, AccessTokens requesterTokens) = await CreateTicketAsRequesterAsync();
        AccessTokens agentTokens = await CreateAgentAsync();

        // Act: o solicitante escreve primeiro.
        Authenticate(requesterTokens.AccessToken);
        (await PostMessageAsync(ticketId, "Any news?")).StatusCode.ShouldBe(HttpStatusCode.OK);
        TicketSlaTimer afterRequester = GetTimers(ticketId).Single(t => t.Metric == SlaMetric.FirstResponse);

        // Depois, o Atendente responde.
        Authenticate(agentTokens.AccessToken);
        HttpResponseMessage agentResponse = await PostMessageAsync(ticketId, "On my way.");
        agentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        Guid agentMessageId = await agentResponse.Content.ReadFromJsonAsync<Guid>();
        TicketSlaTimer afterAgent = GetTimers(ticketId).Single(t => t.Metric == SlaMetric.FirstResponse);

        // O Atendente edita e exclui a mensagem.
        (await HttpClient.PutAsJsonAsync($"ticket-messages/{agentMessageId}", new { content = "On my way, edited." }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await HttpClient.DeleteAsync($"ticket-messages/{agentMessageId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        TicketSlaTimer afterDelete = GetTimers(ticketId).Single(t => t.Metric == SlaMetric.FirstResponse);

        // Assert
        afterRequester.CompletedOnUtc.ShouldBeNull();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DateTime agentMessageAtUtc = await context.TicketMessages.IgnoreQueryFilters()
            .Where(m => m.Id == agentMessageId)
            .Select(m => m.CreatedAtUtc)
            .SingleAsync();

        afterAgent.CompletedOnUtc.ShouldBe(agentMessageAtUtc);
        afterDelete.CompletedOnUtc.ShouldBe(agentMessageAtUtc);

        Authenticate(requesterTokens.AccessToken);
        (await GetSlaAsync(ticketId)).Objectives.Single(o => o.Metric == FirstResponse).State.ShouldBe(Met);
    }

    [Fact]
    public async Task PostMessage_Should_RecordBothMessagesAndSingleCompletion_WhenTwoAgentsAnswerAtOnce()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        AccessTokens firstAgent = await CreateAgentAsync();
        AccessTokens secondAgent = await CreateAgentAsync();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Gatilho temporário que segura por um segundo a conclusão do cronômetro, com a linha bloqueada: a outra
        // mensagem lê a primeira resposta ainda em aberto e tenta concluí-la também, e o conflito acontece sempre.
        await context.Database.ExecuteSqlRawAsync(DelayTimerCompletionSql);

        HttpResponseMessage[] responses;
        int updateAttempts;

        try
        {
            // Act
            responses = await Task.WhenAll(
                SendMessageAsAsync(firstAgent, ticketId, "First agent here."),
                SendMessageAsAsync(secondAgent, ticketId, "Second agent here."));

            updateAttempts = await context.Database
                .SqlQueryRaw<int>("SELECT last_value::integer AS \"Value\" FROM public.test_sla_timer_update_attempts")
                .SingleAsync();
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync(DropDelayTimerCompletionSql);
        }

        // As duas mensagens tentaram concluir a primeira resposta: a segunda entrou em conflito e foi gravada sem ela.
        updateAttempts.ShouldBe(2);

        // Assert
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);

        List<TicketMessage> messages = await context.TicketMessages.AsNoTracking()
            .Where(m => m.TicketId == ticketId)
            .ToListAsync();
        messages.Count.ShouldBe(2);

        TicketSlaTimer firstResponse = GetTimers(ticketId).Single(t => t.Metric == SlaMetric.FirstResponse);
        firstResponse.CompletedOnUtc.ShouldNotBeNull();
        messages.Select(m => m.CreatedAtUtc).ShouldContain(firstResponse.CompletedOnUtc!.Value);

        // Cada mensagem foi auditada uma única vez, e o cronômetro foi alterado uma única vez.
        string[] messageIds = [.. messages.Select(m => m.Id.ToString())];
        (await context.AuditLogs.CountAsync(a => a.EntityName == nameof(TicketMessage) && messageIds.Contains(a.EntityId)))
            .ShouldBe(2);
        (await context.AuditLogs.CountAsync(a => a.EntityName == nameof(TicketSlaTimer)
                                                 && a.EntityId == firstResponse.Id.ToString()
                                                 && a.Action == "Modified"))
            .ShouldBe(1);
    }

    private const string DelayTimerCompletionSql =
        """
        CREATE OR REPLACE FUNCTION public.test_delay_sla_timer_completion() RETURNS trigger AS $$
        BEGIN
            PERFORM pg_sleep(1);
            RETURN NEW;
        END;
        $$ LANGUAGE plpgsql;

        CREATE TRIGGER test_delay_sla_timer_completion
            BEFORE UPDATE ON public.ticket_sla_timers
            FOR EACH ROW
            WHEN (OLD.completed_on_utc IS NULL AND NEW.completed_on_utc IS NOT NULL)
            EXECUTE FUNCTION public.test_delay_sla_timer_completion();

        -- Sequência, e não tabela: o nextval não volta no rollback da gravação que entra em conflito.
        CREATE SEQUENCE public.test_sla_timer_update_attempts START 1 MINVALUE 0;
        SELECT setval('public.test_sla_timer_update_attempts', 0);

        CREATE OR REPLACE FUNCTION public.test_count_sla_timer_update() RETURNS trigger AS $$
        BEGIN
            PERFORM nextval('public.test_sla_timer_update_attempts');
            RETURN NULL;
        END;
        $$ LANGUAGE plpgsql;

        -- Por comando: dispara também quando o UPDATE não encontra a linha (conflito de concorrência).
        CREATE TRIGGER test_count_sla_timer_update
            AFTER UPDATE ON public.ticket_sla_timers
            FOR EACH STATEMENT
            EXECUTE FUNCTION public.test_count_sla_timer_update();
        """;

    private const string DropDelayTimerCompletionSql =
        """
        DROP TRIGGER IF EXISTS test_delay_sla_timer_completion ON public.ticket_sla_timers;
        DROP FUNCTION IF EXISTS public.test_delay_sla_timer_completion();
        DROP TRIGGER IF EXISTS test_count_sla_timer_update ON public.ticket_sla_timers;
        DROP FUNCTION IF EXISTS public.test_count_sla_timer_update();
        DROP SEQUENCE IF EXISTS public.test_sla_timer_update_attempts;
        """;

    [Fact]
    public async Task Cancel_Should_StopTimersAndMakeSlaNotApplicable()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();

        // Act
        await UpdateStatusAsync(ticketId, 5); // Canceled

        // Assert
        SlaDto sla = await GetSlaAsync(ticketId);
        sla.Objectives.ShouldAllBe(o => o.State == Stopped && o.StoppedAtUtc != null && o.CompletedAtUtc == null);

        SlaTicketDetailDto? detail = await HttpClient.GetFromJsonAsync<SlaTicketDetailDto>($"tickets/{ticketId}");
        detail!.SlaStatus.ShouldBe(6); // NotApplicable

        await AuthenticateAsAdminAsync();
        TicketIdsPageDto? breached = await HttpClient.GetFromJsonAsync<TicketIdsPageDto>("tickets?slaStatus=3&pageSize=50");
        breached!.Items.ShouldNotContain(t => t.Id == ticketId);
        TicketIdsPageDto? notApplicable = await HttpClient.GetFromJsonAsync<TicketIdsPageDto>("tickets?slaStatus=6&pageSize=50");
        notApplicable!.Items.ShouldContain(t => t.Id == ticketId);
    }

    [Fact]
    public async Task Reopen_Should_StartSecondResolutionCycle_AndKeepFirstCycleAndDeadlines()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        await UpdateStatusAsync(ticketId, 2); // InProgress
        await UpdateStatusAsync(ticketId, 3); // Resolved
        SlaTicketDetailDto? resolved = await HttpClient.GetFromJsonAsync<SlaTicketDetailDto>($"tickets/{ticketId}");
        SlaObjectiveDto firstResponseBefore = (await GetSlaAsync(ticketId)).Objectives.Single(o => o.Metric == FirstResponse);

        // Act
        DateTime beforeReopenUtc = DateTime.UtcNow;
        (await HttpClient.PostAsync($"tickets/{ticketId}/reopen", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Assert
        SlaDto sla = await GetSlaAsync(ticketId);
        SlaObjectiveDto[] resolutions = [.. sla.Objectives.Where(o => o.Metric == Resolution)];
        resolutions.Select(o => o.Cycle).ShouldBe([1, 2]);

        resolutions[0].State.ShouldBe(Met);
        resolutions[0].IsCurrentCycle.ShouldBeFalse();
        resolutions[1].IsCurrentCycle.ShouldBeTrue();
        resolutions[1].TargetMinutes.ShouldBe(240);
        resolutions[1].StartedAtUtc!.Value.ShouldBeInRange(beforeReopenUtc.AddMilliseconds(-1), DateTime.UtcNow);
        resolutions[1].CompletedAtUtc.ShouldBeNull();

        // A primeira resposta, concluída na resolução, não reabre.
        sla.Objectives.Single(o => o.Metric == FirstResponse).ShouldBe(firstResponseBefore);

        SlaTicketDetailDto? reopened = await HttpClient.GetFromJsonAsync<SlaTicketDetailDto>($"tickets/{ticketId}");
        reopened!.DueAtUtc.ShouldBe(resolved!.DueAtUtc);
        reopened.SlaStatus.ShouldBe(Met);
    }

    [Fact]
    public async Task StatusChange_Should_KeepOnlyWinningChangeOnTimers_WhenTwoChangesConflict()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        DateTime nowUtc = DateTime.UtcNow;

        using IServiceScope firstScope = Services.CreateScope();
        using IServiceScope secondScope = Services.CreateScope();
        ApplicationDbContext first = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationDbContext second = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // As duas mudanças leem o chamado e os cronômetros antes de qualquer uma salvar.
        Ticket ticketToResolve = await first.Tickets.SingleAsync(t => t.Id == ticketId);
        List<TicketSlaTimer> timersToComplete = await first.TicketSlaTimers.Where(t => t.TicketId == ticketId).ToListAsync();
        Ticket ticketToCancel = await second.Tickets.SingleAsync(t => t.Id == ticketId);
        List<TicketSlaTimer> timersToStop = await second.TicketSlaTimers.Where(t => t.TicketId == ticketId).ToListAsync();

        ticketToCancel.Status = TicketStatus.Canceled;
        timersToStop.ForEach(t => t.StoppedOnUtc = nowUtc);
        await second.SaveChangesAsync();

        ticketToResolve.Status = TicketStatus.Resolved;
        ticketToResolve.ResolvedAtUtc = nowUtc;
        timersToComplete.ForEach(t => t.CompletedOnUtc = nowUtc);

        // Act
        Func<Task> losingSave = () => first.SaveChangesAsync();

        // Assert
        await losingSave.ShouldThrowAsync<DbUpdateConcurrencyException>();

        List<TicketSlaTimer> timers = GetTimers(ticketId);
        timers.ShouldAllBe(t => t.StoppedOnUtc != null && t.CompletedOnUtc == null);
    }

    [Fact]
    public async Task Timers_Should_BeAudited_WithoutChangingStartAfterwards()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        await UpdateStatusAsync(ticketId, 2); // InProgress
        await UpdateStatusAsync(ticketId, 3); // Resolved

        // Act
        List<TicketSlaTimer> timers = GetTimers(ticketId);
        string[] timerIds = [.. timers.Select(t => t.Id.ToString())];

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<AuditLogEntry> entries = await context.AuditLogs
            .Where(a => a.EntityName == nameof(TicketSlaTimer) && timerIds.Contains(a.EntityId))
            .ToListAsync();

        // Assert
        entries.Count(a => a.Action == "Added").ShouldBe(2);

        List<AuditLogEntry> modified = [.. entries.Where(a => a.Action == "Modified")];
        modified.Count.ShouldBe(2);
        modified.ShouldAllBe(a => a.AffectedColumns == nameof(TicketSlaTimer.CompletedOnUtc));
    }

    [Fact]
    public async Task Migration_Should_GiveLegacyPlanAndSingleResolutionTimer_ToExistingTicketsWithoutChangingSla()
    {
        // Arrange: chamados criados antes da migration, sem plano, num banco que volta ao estado anterior no fim.
        Guid sectorId = await CreateSectorAsAdminAsync();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid requesterId = await context.Users.Where(u => u.Email == AdminEmail).Select(u => u.Id).SingleAsync();
        int calendarCountBefore = await context.SlaCalendars.CountAsync();

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.tickets ALTER COLUMN sla_plan DROP NOT NULL;");

        var openedAtUtc = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var openId = Guid.NewGuid();
        var resolvedId = Guid.NewGuid();
        var canceledId = Guid.NewGuid();
        DateTime resolvedAtUtc = openedAtUtc.AddHours(2);
        DateTime canceledAtUtc = openedAtUtc.AddHours(1);

        await InsertLegacyTicketAsync(context, openId, requesterId, sectorId, "Urgent", "Open", openedAtUtc, null);
        await InsertLegacyTicketAsync(context, resolvedId, requesterId, sectorId, "High", "Closed", openedAtUtc, resolvedAtUtc);
        await InsertLegacyTicketAsync(context, canceledId, requesterId, sectorId, "Low", "Canceled", openedAtUtc, null);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.ticket_histories (id, ticket_id, changed_by_user_id, action, old_value, new_value, occurred_at_utc)
            VALUES ({Guid.NewGuid()}, {canceledId}, {requesterId}, 'StatusChanged', 'Open', 'Canceled', {canceledAtUtc});
            """);

        Guid[] ticketIds = [openId, resolvedId, canceledId];
        List<(Guid, TicketStatus, DateTime, DateTime, DateTime?)> slaColumnsBefore = await GetSlaColumnsAsync(context, ticketIds);

        // Act: o preenchimento roda duas vezes.
        for (int run = 0; run < 2; run++)
        {
            await context.Database.ExecuteSqlRawAsync(Add_Ticket_Sla_Timers.BackfillSlaPlansSql);
            await context.Database.ExecuteSqlRawAsync(Add_Ticket_Sla_Timers.BackfillResolutionTimersSql);
        }

        // Assert
        List<TicketSlaTimer> timers = await context.TicketSlaTimers.AsNoTracking()
            .Where(t => ticketIds.Contains(t.TicketId))
            .ToListAsync();

        timers.Count.ShouldBe(3);
        timers.ShouldAllBe(t => t.Metric == SlaMetric.Resolution && t.Cycle == 1 && t.CalendarId == SlaCalendarIds.Continuous);

        TicketSlaTimer open = timers.Single(t => t.TicketId == openId);
        open.TargetMinutes.ShouldBe(240);
        open.StartedAtUtc.ShouldBe(openedAtUtc);
        open.DueOnUtc.ShouldBe(openedAtUtc.AddHours(4));
        open.WarningOnUtc.ShouldBe(openedAtUtc.AddHours(3));
        open.CompletedOnUtc.ShouldBeNull();
        open.StoppedOnUtc.ShouldBeNull();

        TicketSlaTimer resolved = timers.Single(t => t.TicketId == resolvedId);
        resolved.TargetMinutes.ShouldBe(480);
        resolved.CompletedOnUtc.ShouldBe(resolvedAtUtc);

        TicketSlaTimer canceled = timers.Single(t => t.TicketId == canceledId);
        canceled.StoppedOnUtc.ShouldBe(canceledAtUtc);
        canceled.CompletedOnUtc.ShouldBeNull();

        List<TicketSlaPlan> plans = await context.Tickets.IgnoreQueryFilters()
            .Where(t => ticketIds.Contains(t.Id))
            .Select(t => t.SlaPlan)
            .ToListAsync();
        plans.ShouldAllBe(p => p.IsLegacy && p.PolicyName == "Legado (horas corridas)" && p.FirstResponseMinutes == null);

        (await GetSlaColumnsAsync(context, ticketIds)).ShouldBe(slaColumnsBefore);
        (await context.SlaCalendars.CountAsync()).ShouldBe(calendarCountBefore);

        await transaction.RollbackAsync();
    }

    private static async Task InsertLegacyTicketAsync(
        ApplicationDbContext context,
        Guid id,
        Guid requesterId,
        Guid sectorId,
        string priority,
        string status,
        DateTime createdAtUtc,
        DateTime? resolvedAtUtc)
    {
        TimeSpan resolutionTime = TicketSla.ResolutionTime(Enum.Parse<TicketPriority>(priority));
        DateTime dueAtUtc = createdAtUtc + resolutionTime;
        DateTime warningAtUtc = createdAtUtc + resolutionTime * TicketSla.WarningRatio;

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.tickets (id, title, description, priority, status, created_by_user_id, sector_id,
                created_at_utc, due_at_utc, sla_warning_at_utc, resolved_at_utc)
            VALUES ({id}, 'Legacy ticket', 'Opened before the SLA plan', {priority}, {status}, {requesterId}, {sectorId},
                {createdAtUtc}, {dueAtUtc}, {warningAtUtc}, {resolvedAtUtc});
            """);
    }

    // As colunas de que dependem o SlaStatus, o resumo e os filtros por situação do SLA.
    private static async Task<List<(Guid, TicketStatus, DateTime, DateTime, DateTime?)>> GetSlaColumnsAsync(
        ApplicationDbContext context,
        Guid[] ticketIds)
    {
        var rows = await context.Tickets.IgnoreQueryFilters()
            .Where(t => ticketIds.Contains(t.Id))
            .OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.Status, t.DueAtUtc, t.SlaWarningAtUtc, t.ResolvedAtUtc })
            .ToListAsync();

        return [.. rows.Select(r => (r.Id, r.Status, r.DueAtUtc, r.SlaWarningAtUtc, r.ResolvedAtUtc))];
    }

    private sealed record SlaPlanDto(
        Guid? PolicyId,
        string PolicyName,
        int PolicyVersion,
        Guid CalendarId,
        string CalendarName,
        bool IsLegacy);

    private sealed record SlaObjectiveDto(
        int Metric,
        int Cycle,
        int State,
        int? TargetMinutes,
        DateTime? StartedAtUtc,
        DateTime? WarningAtUtc,
        DateTime? DueAtUtc,
        DateTime? CompletedAtUtc,
        DateTime? StoppedAtUtc,
        int? ConsumedMinutes,
        int? RemainingMinutes,
        bool IsCurrentCycle);

    private sealed record SlaDto(DateTime ServerNowUtc, SlaPlanDto Plan, List<SlaObjectiveDto> Objectives);

    private sealed record SlaTicketDetailDto(
        Guid Id,
        string Title,
        DateTime DueAtUtc,
        DateTime SlaWarningAtUtc,
        DateTime? ResolvedAtUtc,
        int SlaStatus,
        SlaDto Sla);

    private sealed record TicketIdDto(Guid Id);

    private sealed record TicketIdsPageDto(List<TicketIdDto> Items);
}
