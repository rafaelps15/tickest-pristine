using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Application.SlaCalendars;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using TickestPristine.Application.Roles;
using TickestPristine.Domain.Audit;
using TickestPristine.Infrastructure.Database.Migrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using TickestPristine.Infrastructure.Tickets.SlaMonitoring;

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
            status = 1 // Open
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        TicketDetailDto? ticket = await HttpClient.GetFromJsonAsync<TicketDetailDto>($"tickets/{ticketId}");
        ticket!.Description.ShouldBe("Updated description");
        ticket.Status.ShouldBe(1); // Open
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

        await ResolveTicketAsync(ticketId);

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

        await ResolveTicketAsync(ticketId);

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
    public async Task GetOwn_Should_ReturnAssigneeSectorDueAndSlaFields_LikeGetAll()
    {
        // Arrange: um chamado de Maria atribuído ao administrador e outro sem responsável, no mesmo setor
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid requesterId, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid assignedId = await CreateTicketAsync(sectorId);
        Guid unassignedId = await CreateTicketAsync(sectorId);

        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        (await HttpClient.PutAsJsonAsync($"tickets/{assignedId}/assignee", new { assignedToUserId = GetUserId(adminTokens) }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        PagedTicketsDto? general = await HttpClient.GetFromJsonAsync<PagedTicketsDto>($"tickets?requesterId={requesterId}");
        string sectorName = await GetSectorNameAsync(sectorId);

        // Act
        Authenticate(requesterTokens.AccessToken);
        PagedOwnTicketItemsDto? page = await HttpClient.GetFromJsonAsync<PagedOwnTicketItemsDto>("tickets/mine");

        // Assert
        OwnTicketItemDto assigned = page!.Items.Single(t => t.Id == assignedId);
        TicketSummaryDto sameInGetAll = general!.Items.Single(t => t.Id == assignedId);
        assigned.AssignedToName.ShouldBe("Admin Master");
        assigned.SectorId.ShouldBe(sectorId);
        assigned.SectorName.ShouldBe(sectorName);
        assigned.DueAtUtc.ShouldBe(sameInGetAll.DueAtUtc);
        assigned.SlaStatus.ShouldBe(sameInGetAll.SlaStatus);
        assigned.Title.ShouldBe("Printer is broken");
        assigned.Description.ShouldBe("The printer on the 3rd floor is not working");
        assigned.Priority.ShouldBe(2); // Medium
        assigned.OpenedByUserId.ShouldBe(requesterId);

        page.Items.Single(t => t.Id == unassignedId).AssignedToName.ShouldBeNull();
    }

    [Fact]
    public async Task GetOwn_Should_FilterByStatusGroup_WhenStatusIsGiven()
    {
        // Arrange: um chamado de Maria em cada status
        (Guid requesterId, Guid sectorId) = await CreateRequesterAndSectorAsync();
        foreach (TicketStatus status in Enum.GetValues<TicketStatus>())
        {
            await InsertOwnTicketAsync(requesterId, sectorId, status, "Printer is broken", DateTime.UtcNow.AddHours(-1));
        }

        // Act
        PagedOwnTicketItemsDto? active = await HttpClient.GetFromJsonAsync<PagedOwnTicketItemsDto>("tickets/mine?status=Active");
        PagedOwnTicketItemsDto? finished = await HttpClient.GetFromJsonAsync<PagedOwnTicketItemsDto>("tickets/mine?status=Finished");
        PagedOwnTicketItemsDto? all = await HttpClient.GetFromJsonAsync<PagedOwnTicketItemsDto>("tickets/mine");
        HttpResponseMessage invalid = await HttpClient.GetAsync("tickets/mine?status=xyz");

        // Assert
        active!.Items.Select(t => t.Status).Order().ShouldBe([1, 2]); // Open, InProgress
        finished!.Items.Select(t => t.Status).Order().ShouldBe([3, 4, 5]); // Resolved, Closed, Canceled
        all!.TotalCount.ShouldBe(5);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOwn_Should_SearchTitleIgnoringCase_WhenSearchIsGiven()
    {
        // Arrange
        (Guid requesterId, Guid sectorId) = await CreateRequesterAndSectorAsync();
        Guid printerId = await InsertOwnTicketAsync(requesterId, sectorId, TicketStatus.Open, "Impressora não imprime", DateTime.UtcNow.AddHours(-1));
        Guid emailId = await InsertOwnTicketAsync(requesterId, sectorId, TicketStatus.Open, "Acesso ao e-mail", DateTime.UtcNow.AddHours(-2));

        // Act
        PagedOwnTicketItemsDto? byUpperCase = await HttpClient.GetFromJsonAsync<PagedOwnTicketItemsDto>("tickets/mine?search=IMPRESS");
        PagedOwnTicketItemsDto? bySpaces = await HttpClient.GetFromJsonAsync<PagedOwnTicketItemsDto>("tickets/mine?search=%20%20e-mail%20%20");
        PagedOwnTicketItemsDto? byBlank = await HttpClient.GetFromJsonAsync<PagedOwnTicketItemsDto>("tickets/mine?search=%20%20%20");

        // Assert
        byUpperCase!.Items.ShouldHaveSingleItem().Id.ShouldBe(printerId);
        bySpaces!.Items.ShouldHaveSingleItem().Id.ShouldBe(emailId);
        byBlank!.Items.Select(t => t.Id).ShouldBe([printerId, emailId], ignoreOrder: true);
    }

    [Fact]
    public async Task GetOwn_Should_PageFilteredResultsByLastActivity_WhenTicketGetsNewMessage()
    {
        // Arrange: A, B e C abertos nessa ordem; depois, uma mensagem nova em A
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        Guid a = await CreateTicketAsync(sectorId);
        Guid b = await CreateTicketAsync(sectorId);
        Guid c = await CreateTicketAsync(sectorId);
        (await PostMessageAsync(a, "Alguma novidade?")).EnsureSuccessStatusCode();

        // Act
        PagedOwnTicketItemsDto? firstPage = await HttpClient.GetFromJsonAsync<PagedOwnTicketItemsDto>(
            "tickets/mine?status=Active&pageSize=2");
        PagedOwnTicketItemsDto? secondPage = await HttpClient.GetFromJsonAsync<PagedOwnTicketItemsDto>(
            "tickets/mine?status=Active&pageSize=2&page=2");

        // Assert
        firstPage!.Items.Select(t => t.Id).ShouldBe([a, c]);
        firstPage.TotalCount.ShouldBe(3);
        secondPage!.Items.Select(t => t.Id).ShouldBe([b]);
    }

    [Fact]
    public async Task GetOwn_Should_ReturnOnlyCallerTickets_WhenOtherUserHasTicketWithSameTitle()
    {
        // Arrange: Paulo e Maria com chamados de mesmo título
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens paulo) = await RegisterAndLoginAsync();
        Authenticate(paulo.AccessToken);
        Guid paulosTicketId = await CreateTicketAsync(sectorId);
        (_, AccessTokens maria) = await RegisterAndLoginAsync();
        Authenticate(maria.AccessToken);
        Guid mariasTicketId = await CreateTicketAsync(sectorId);

        // Act
        PagedOwnTicketItemsDto? page = await HttpClient.GetFromJsonAsync<PagedOwnTicketItemsDto>(
            "tickets/mine?search=Printer");

        // Assert
        page!.Items.ShouldHaveSingleItem().Id.ShouldBe(mariasTicketId);
        page.Items.ShouldNotContain(t => t.Id == paulosTicketId);
    }

    [Fact]
    public async Task GetOwn_Should_ReturnForbidden_WithoutViewOwnPermission()
    {
        // Arrange: usuário sem nenhuma role, portanto sem tickets:view-own
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens userTokens = await LoginAsync(email);

        await AuthenticateAsAdminAsync();
        (await HttpClient.PutAsJsonAsync($"users/{userId}/roles", new { roleNames = Array.Empty<string>() }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        Authenticate(userTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("tickets/mine");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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
        await AuthenticateAsAdminAsync();

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
            .SingleAsync(h => h.TicketId == ticketId && h.Action == TicketHistoryAction.Assigned);
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
        (Guid ticketId, Guid adminId) = await CreateTicketAssignedToAdminAsync();
        HttpResponseMessage unassignResponse = await HttpClient.DeleteAsync($"tickets/{ticketId}/assignee");
        unassignResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        string adminName = GetFullName(adminId);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets/{ticketId}/history");

        // Assert
        response.EnsureSuccessStatusCode();
        List<TicketHistoryDto>? history = (await response.Content.ReadFromJsonAsync<CursorPageDto<TicketHistoryDto>>())?.Items;
        history.ShouldNotBeNull();
        history.ShouldContain(h =>
            h.Action == (int)TicketHistoryAction.Unassigned &&
            h.Description == $"Responsável removido: {adminName}");
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

    [Fact]
    public async Task Cancel_Should_ReturnNoContentAndRecordHistory_WhenOwnerCancelsWithinWindow()
    {
        // Arrange: chamado de Maria aberto há 14 minutos ("às 9h14")
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        await SetOpenedAgoAsync(ticketId, TimeSpan.FromMinutes(14));

        // Act
        HttpResponseMessage response = await CancelAsync(ticketId);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        GetStoredStatus(ticketId).ShouldBe(TicketStatus.Canceled);
        GetHistoryAfterCreation(ticketId).ShouldHaveSingleItem().Action.ShouldBe(TicketHistoryAction.StatusChanged);
    }

    [Fact]
    public async Task Cancel_Should_ReturnCancelWindowExpiredAndChangeNothing_WhenOwnerCancelsAfterWindow()
    {
        // Arrange: chamado de Maria aberto há 16 minutos ("às 9h16")
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        await SetOpenedAgoAsync(ticketId, TimeSpan.FromMinutes(16));

        // Act
        HttpResponseMessage response = await CancelAsync(ticketId);

        // Assert
        await ShouldBeForbiddenAsync(response, "Tickets.CancelWindowExpired");
        GetStoredStatus(ticketId).ShouldBe(TicketStatus.Open);
        GetHistoryAfterCreation(ticketId).ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancel_Should_ReturnCancelWindowExpired_WhenOwnerCancelsReopenedTicketAfterWindow()
    {
        // Arrange: cancelado e reaberto dentro da janela; depois, a abertura fica 20 minutos para trás ("às 9h20")
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        (await CancelAsync(ticketId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await HttpClient.PostAsync($"tickets/{ticketId}/reopen", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await SetOpenedAgoAsync(ticketId, TimeSpan.FromMinutes(20));

        // Act
        HttpResponseMessage response = await CancelAsync(ticketId);

        // Assert
        await ShouldBeForbiddenAsync(response, "Tickets.CancelWindowExpired");
        GetStoredStatus(ticketId).ShouldBe(TicketStatus.Open);
    }

    [Fact]
    public async Task Cancel_Should_AllowOnlyCancelPermission_WhenTicketIsTwoHoursOld()
    {
        // Arrange: Ana (Atendente, com tickets:manage e sem tickets:cancel) e o administrador
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        await SetOpenedAgoAsync(ticketId, TimeSpan.FromHours(2));
        AccessTokens anaTokens = await CreateAgentAsync();

        // Act
        Authenticate(anaTokens.AccessToken);
        HttpResponseMessage byAna = await CancelAsync(ticketId);
        TicketStatus statusAfterAna = GetStoredStatus(ticketId);
        await AuthenticateAsAdminAsync();
        HttpResponseMessage byAdministrator = await CancelAsync(ticketId);

        // Assert
        await ShouldBeForbiddenAsync(byAna, "Tickets.CancelForbidden");
        statusAfterAna.ShouldBe(TicketStatus.Open);
        byAdministrator.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        GetStoredStatus(ticketId).ShouldBe(TicketStatus.Canceled);
    }

    [Fact]
    public async Task Cancel_Should_ReturnNoContent_WhenRoleWasGrantedCancelPermission()
    {
        // Arrange: uma role com tickets:cancel, concedida pelo administrador, e um usuário só com ela
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        await SetOpenedAgoAsync(ticketId, TimeSpan.FromHours(2));

        string roleName = await CreateTestRoleAsync();
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await AuthenticateAsAdminAsync();
        (await HttpClient.PutAsJsonAsync($"roles/{roleName}/permissions", new { permissionCodes = CancelPermissionOnly }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await HttpClient.PutAsJsonAsync($"users/{userId}/roles", new { roleNames = new[] { roleName } }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Authenticate((await LoginAsync(email)).AccessToken);

        // Act
        HttpResponseMessage response = await CancelAsync(ticketId);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        GetStoredStatus(ticketId).ShouldBe(TicketStatus.Canceled);
    }

    [Fact]
    public async Task Update_Should_ForbidStatusChangeButAllowDescriptionEdit_WhenOwnerUpdatesOwnTicket()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();

        // Act
        HttpResponseMessage toInProgress = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "The printer on the 3rd floor is not working",
            status = 2 // InProgress
        });
        TicketStatus statusAfterAttempt = GetStoredStatus(ticketId);
        HttpResponseMessage descriptionOnly = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "The printer prints blank pages",
            status = 1 // Open
        });

        // Assert
        await ShouldBeForbiddenAsync(toInProgress, "Tickets.StatusChangeForbidden");
        statusAfterAttempt.ShouldBe(TicketStatus.Open);
        descriptionOnly.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        TicketDetailDto? ticket = await HttpClient.GetFromJsonAsync<TicketDetailDto>($"tickets/{ticketId}");
        ticket!.Description.ShouldBe("The printer prints blank pages");
        ticket.Status.ShouldBe(1); // Open
    }

    [Fact]
    public async Task Delete_Should_AllowOnlyDeletePermission_WhenOwnerAgentAndAdministratorTry()
    {
        // Arrange
        (Guid ticketId, AccessTokens mariaTokens) = await CreateTicketAsRequesterAsync();
        AccessTokens anaTokens = await CreateAgentAsync();

        // Act
        Authenticate(mariaTokens.AccessToken);
        HttpResponseMessage byMaria = await HttpClient.DeleteAsync($"tickets/{ticketId}");
        Authenticate(anaTokens.AccessToken);
        HttpResponseMessage byAna = await HttpClient.DeleteAsync($"tickets/{ticketId}");
        bool deletedBeforeAdministrator = IsDeleted(ticketId);
        await AuthenticateAsAdminAsync();
        HttpResponseMessage byAdministrator = await HttpClient.DeleteAsync($"tickets/{ticketId}");

        // Assert
        byMaria.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        byAna.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        deletedBeforeAdministrator.ShouldBeFalse();
        byAdministrator.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        IsDeleted(ticketId).ShouldBeTrue();
    }

    [Fact]
    public async Task Migration_Should_ReplaceDeleteOwnWithCancelAndDelete_IncludingManualGrants()
    {
        // Arrange: o banco antes da migration, com tickets:delete-own também numa role criada à mão
        string roleName = await CreateTestRoleAsync();

        List<string> grantedBeforeMigration = [];

        await ExecuteInScopeAsync(async context =>
        {
            IMigrator migrator = context.GetService<IMigrator>();
            var migrations = context.Database.GetMigrations().ToList();
            int index = migrations.FindIndex(m =>
                m.EndsWith(nameof(Replace_Ticket_Delete_Own_With_Cancel_And_Delete_Permissions), StringComparison.Ordinal));

            try
            {
                await migrator.MigrateAsync(migrations[index - 1]);
                await context.Database.ExecuteSqlAsync(
                    $"INSERT INTO public.role_permissions (id, role_name, permission_code) VALUES ({Guid.NewGuid()}, {roleName}, 'tickets:delete-own')");
                grantedBeforeMigration = await context.RolePermissions.AsNoTracking()
                    .Where(rp => rp.PermissionCode == "tickets:delete-own")
                    .Select(rp => rp.RoleName)
                    .ToListAsync();

                // Act
                await migrator.MigrateAsync(migrations[index]);
            }
            finally
            {
                // O banco é compartilhado pela coleção: volta à última migration, mesmo se o teste falhar no meio.
                await migrator.MigrateAsync();
            }
        });

        // Assert
        grantedBeforeMigration.ShouldBe(
            [RoleNames.Administrator, RoleNames.Agent, RoleNames.Collaborator, roleName],
            ignoreOrder: true);

        await ExecuteInScopeAsync(async context =>
        {
            (await context.Permissions.AnyAsync(p => p.Code == "tickets:delete-own")).ShouldBeFalse();
            (await context.RolePermissions.AnyAsync(rp => rp.PermissionCode == "tickets:delete-own")).ShouldBeFalse();

            foreach (string code in NewTicketPermissions)
            {
                (await context.Permissions.SingleAsync(p => p.Code == code)).IsAdministrative.ShouldBeTrue();
                (await context.RolePermissions.Where(rp => rp.PermissionCode == code).Select(rp => rp.RoleName).ToListAsync())
                    .ShouldBe([RoleNames.Administrator]);
            }
        });
    }

    [Fact]
    public async Task Reopen_Should_PostTrimmedReasonAsMessage_WhenReasonIsGiven()
    {
        // Arrange
        (Guid ticketId, AccessTokens mariaTokens) = await CreateTicketAsRequesterAsync();
        await ResolveTicketAsync(ticketId);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"tickets/{ticketId}/reopen",
            new { reason = "  A impressora parou de novo.  " });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        GetStoredStatus(ticketId).ShouldBe(TicketStatus.Open);

        TicketMessage message = GetMessages(ticketId).ShouldHaveSingleItem();
        message.Content.ShouldBe("A impressora parou de novo.");
        message.AuthorUserId.ShouldBe(GetUserId(mariaTokens));

        var actions = GetHistoryAfterCreation(ticketId).Select(h => h.Action).ToList();
        actions.ShouldContain(TicketHistoryAction.Reopened);
        actions.ShouldContain(TicketHistoryAction.MessageAdded);
    }

    [Fact]
    public async Task Reopen_Should_SkipBlankReasonAndRejectLongReason_WhenReasonIsMissingBlankOrTooLong()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        await ResolveTicketAsync(ticketId);

        // Act: sem corpo, com motivo em branco e com 501 caracteres, resolvendo de novo entre as tentativas
        HttpResponseMessage withoutBody = await HttpClient.PostAsync($"tickets/{ticketId}/reopen", null);
        await ResolveTicketAsync(ticketId);
        HttpResponseMessage blankReason = await HttpClient.PostAsJsonAsync($"tickets/{ticketId}/reopen", new { reason = "   " });
        await ResolveTicketAsync(ticketId);
        HttpResponseMessage longReason = await HttpClient.PostAsJsonAsync(
            $"tickets/{ticketId}/reopen",
            new { reason = new string('a', 501) });

        // Assert
        withoutBody.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        blankReason.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        longReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        GetStoredStatus(ticketId).ShouldBe(TicketStatus.Resolved);
        GetMessages(ticketId).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetById_And_GetOwn_Should_ReturnCanCancelForCaller_WithinAndAfterOwnerWindow()
    {
        // Arrange: chamado de Maria aberto há 10 minutos ("às 9h10")
        (Guid ticketId, AccessTokens mariaTokens) = await CreateTicketAsRequesterAsync();
        await SetOpenedAgoAsync(ticketId, TimeSpan.FromMinutes(10));

        // Act: Maria às 9h10; depois a abertura fica 20 minutos para trás ("às 9h20"), Maria e o administrador
        CancelInfoDto earlyDetail = (await HttpClient.GetFromJsonAsync<CancelInfoDto>($"tickets/{ticketId}"))!;
        CancelInfoDto earlyItem = await GetOwnCancelInfoAsync(ticketId);

        await SetOpenedAgoAsync(ticketId, TimeSpan.FromMinutes(20));
        CancelInfoDto lateDetail = (await HttpClient.GetFromJsonAsync<CancelInfoDto>($"tickets/{ticketId}"))!;
        CancelInfoDto lateItem = await GetOwnCancelInfoAsync(ticketId);

        await AuthenticateAsAdminAsync();
        CancelInfoDto administratorDetail = (await HttpClient.GetFromJsonAsync<CancelInfoDto>($"tickets/{ticketId}"))!;
        Authenticate(mariaTokens.AccessToken);

        // Assert
        earlyDetail.CanCancel.ShouldBeTrue();
        earlyItem.CanCancel.ShouldBeTrue();
        lateDetail.CanCancel.ShouldBeFalse();
        lateItem.CanCancel.ShouldBeFalse();
        administratorDetail.CanCancel.ShouldBeTrue();

        DateTime expectedDeadline = lateDetail.CreatedAtUtc.AddMinutes(15);
        lateDetail.OwnerCancelDeadlineUtc.ShouldBe(expectedDeadline);
        lateItem.OwnerCancelDeadlineUtc.ShouldBe(expectedDeadline);
        administratorDetail.OwnerCancelDeadlineUtc.ShouldBe(expectedDeadline);
        earlyDetail.OwnerCancelDeadlineUtc.ShouldBe(earlyDetail.CreatedAtUtc.AddMinutes(15));
    }

    [Fact]
    public async Task GetById_Should_ReturnCannotCancel_WhenTicketIsCanceled()
    {
        // Arrange
        (Guid ticketId, AccessTokens mariaTokens) = await CreateTicketAsRequesterAsync();
        (await CancelAsync(ticketId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        CancelInfoDto asMaria = (await HttpClient.GetFromJsonAsync<CancelInfoDto>($"tickets/{ticketId}"))!;
        await AuthenticateAsAdminAsync();
        CancelInfoDto asAdministrator = (await HttpClient.GetFromJsonAsync<CancelInfoDto>($"tickets/{ticketId}"))!;
        Authenticate(mariaTokens.AccessToken);

        // Assert
        asMaria.CanCancel.ShouldBeFalse();
        asAdministrator.CanCancel.ShouldBeFalse();
    }

    private static readonly string[] CancelPermissionOnly = ["tickets:cancel"];

    private static readonly string[] NewTicketPermissions = ["tickets:cancel", "tickets:delete"];

    private async Task<HttpResponseMessage> CancelAsync(Guid ticketId) =>
        await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "The printer on the 3rd floor is not working",
            status = 5 // Canceled
        });

    /// <summary>
    /// Desloca a abertura do chamado para o passado: a API de teste usa o relógio real, e a janela de cancelamento conta
    /// da abertura (spec 016).
    /// </summary>
    private async Task SetOpenedAgoAsync(Guid ticketId, TimeSpan ago)
    {
        DateTime openedAtUtc = DateTime.UtcNow - ago;

        await ExecuteInScopeAsync(context => context.Database.ExecuteSqlAsync(
            $"UPDATE public.tickets SET created_at_utc = {openedAtUtc} WHERE id = {ticketId}"));
    }

    private TicketStatus GetStoredStatus(Guid ticketId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return context.Tickets.IgnoreQueryFilters().Where(t => t.Id == ticketId).Select(t => t.Status).Single();
    }

    private bool IsDeleted(Guid ticketId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return context.Tickets.IgnoreQueryFilters().Where(t => t.Id == ticketId).Select(t => t.DeletedAtUtc != null).Single();
    }

    private List<TicketMessage> GetMessages(Guid ticketId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return context.TicketMessages.AsNoTracking().Where(m => m.TicketId == ticketId).ToList();
    }

    private async Task ExecuteInScopeAsync(Func<ApplicationDbContext, Task> action)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await action(context);
    }

    private async Task<CancelInfoDto> GetOwnCancelInfoAsync(Guid ticketId)
    {
        PagedCancelInfoDto? page = await HttpClient.GetFromJsonAsync<PagedCancelInfoDto>("tickets/mine?pageSize=100");

        return page!.Items.Single(t => t.Id == ticketId);
    }

    private static async Task ShouldBeForbiddenAsync(HttpResponseMessage response, string code)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe(code);
    }

    private sealed record CancelInfoDto(Guid Id, DateTime CreatedAtUtc, bool CanCancel, DateTime OwnerCancelDeadlineUtc);

    private sealed record PagedCancelInfoDto(List<CancelInfoDto> Items, int TotalCount);

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
    /// Leva o chamado de Aberto a Resolvido pela API, como administrador, e devolve o token que estava configurado: desde
    /// a spec 016, só quem atende a fila move o chamado para Em Andamento e Resolvido.
    /// </summary>
    private async Task ResolveTicketAsync(Guid ticketId)
    {
        await RunAsAdministratorAsync(async () =>
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
        });
    }

    /// <summary>
    /// Executa a ação autenticado como administrador e volta ao token que estava configurado.
    /// </summary>
    private async Task RunAsAdministratorAsync(Func<Task> action)
    {
        AuthenticationHeaderValue? previous = HttpClient.DefaultRequestHeaders.Authorization;
        await AuthenticateAsAdminAsync();

        try
        {
            await action();
        }
        finally
        {
            HttpClient.DefaultRequestHeaders.Authorization = previous;
        }
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

    /// <summary>
    /// Chamado do solicitante gravado direto no banco, com o status e o título escolhidos (pela API, o chamado nasce
    /// Aberto) e a última atividade igual à abertura, como o interceptor faria.
    /// </summary>
    private async Task<Guid> InsertOwnTicketAsync(
        Guid requesterId,
        Guid sectorId,
        TicketStatus status,
        string title,
        DateTime createdAtUtc)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "The printer on the 3rd floor is not working",
            Priority = TicketPriority.Medium,
            Status = status,
            CreatedByUserId = requesterId,
            SectorId = sectorId,
            CreatedAtUtc = createdAtUtc,
            LastActivityAtUtc = createdAtUtc,
            DueAtUtc = createdAtUtc.AddHours(8),
            SlaWarningAtUtc = createdAtUtc.AddHours(6),
            SlaPlan = TicketSla.LegacyPlan(TicketPriority.Medium)
        };

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }

    private async Task<string> GetSectorNameAsync(Guid sectorId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.Sectors.Where(s => s.Id == sectorId).Select(s => s.Name).SingleAsync();
    }

    private sealed record OwnTicketItemDto(
        Guid Id,
        string Title,
        string Description,
        int Priority,
        int Status,
        Guid OpenedByUserId,
        string? AssignedToName,
        Guid SectorId,
        string SectorName,
        DateTime DueAtUtc,
        int SlaStatus,
        DateTime LastActivityAtUtc);

    private sealed record PagedOwnTicketItemsDto(List<OwnTicketItemDto> Items, int TotalCount);

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

    [Fact]
    public async Task GetAll_Should_FilterByLocalCreationDay_WhenCreatedPeriodIsGiven()
    {
        // Arrange
        PeriodScenario scenario = await SeedPeriodScenarioAsync();

        // Act
        PagedTicketsDto? page = await HttpClient.GetFromJsonAsync<PagedTicketsDto>(
            $"tickets?sectorId={scenario.Helpdesk}&createdFrom=2026-11-01&createdTo=2026-11-01");

        // Assert: o de 31/10 às 23h30 de Brasília fica fora, mesmo sendo 01/11 em UTC
        page!.Items.ShouldHaveSingleItem().Id.ShouldBe(scenario.MorningOfFirst);
        page.Items.ShouldNotContain(t => t.Id == scenario.LateEveningBefore);
    }

    [Fact]
    public async Task GetAll_Should_FilterFromCreationDayOnward_WhenOnlyCreatedFromIsGiven()
    {
        // Arrange
        PeriodScenario scenario = await SeedPeriodScenarioAsync();

        // Act
        PagedTicketsDto? page = await HttpClient.GetFromJsonAsync<PagedTicketsDto>(
            $"tickets?sectorId={scenario.Helpdesk}&createdFrom=2026-11-01");

        // Assert
        page!.Items.Select(t => t.Id).ShouldBe([scenario.MorningOfFirst, scenario.MidMonth], ignoreOrder: true);
    }

    [Fact]
    public async Task GetAll_Should_ReturnOnlyTicketsResolvedInPeriod_WhenResolvedPeriodIsGiven()
    {
        // Arrange
        PeriodScenario scenario = await SeedPeriodScenarioAsync();

        // Act
        PagedTicketsDto? page = await HttpClient.GetFromJsonAsync<PagedTicketsDto>(
            $"tickets?sectorId={scenario.Helpdesk}&resolvedFrom=2026-11-02&resolvedTo=2026-11-02");

        // Assert: os nunca resolvidos ficam de fora
        page!.Items.ShouldHaveSingleItem().Id.ShouldBe(scenario.MorningOfFirst);
        page.TotalCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("createdFrom=2026-11-10&createdTo=2026-11-01")]
    [InlineData("resolvedFrom=2026-11-10&resolvedTo=2026-11-01")]
    public async Task GetAll_Should_ReturnBadRequest_WhenPeriodIsInverted(string period)
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"tickets?{period}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Tickets.InvalidPeriod");
        body.ShouldContain("A data inicial do período não pode ser posterior à data final.");
    }

    [Fact]
    public async Task GetAll_Should_ReturnTicketsOfEverySector_WhenSectorIdIsRepeated()
    {
        // Arrange
        PeriodScenario scenario = await SeedPeriodScenarioAsync();

        // Act
        PagedTicketsDto? both = await HttpClient.GetFromJsonAsync<PagedTicketsDto>(
            $"tickets?sectorId={scenario.Helpdesk}&sectorId={scenario.Infra}");
        PagedTicketsDto? helpdeskOnly = await HttpClient.GetFromJsonAsync<PagedTicketsDto>(
            $"tickets?sectorId={scenario.Helpdesk}");

        // Assert
        both!.Items.Select(t => t.Id).ShouldBe(
            [scenario.LateEveningBefore, scenario.MorningOfFirst, scenario.MidMonth, scenario.InInfra],
            ignoreOrder: true);
        helpdeskOnly!.Items.Select(t => t.Id).ShouldBe(
            [scenario.LateEveningBefore, scenario.MorningOfFirst, scenario.MidMonth],
            ignoreOrder: true);
    }

    /// <summary>
    /// Chamados da spec 025, em setores novos (isolam os dados dos outros testes): no Helpdesk, um aberto em 31/10 às
    /// 23h30 de Brasília (02h30 UTC de 01/11), um em 01/11 às 10h resolvido em 02/11 e um em 15/11 nunca resolvido;
    /// na Infra, um aberto em 01/11 às 10h. Termina autenticado como administrador (<c>tickets:manage</c>).
    /// </summary>
    private async Task<PeriodScenario> SeedPeriodScenarioAsync()
    {
        Guid helpdesk = await CreateSectorAsAdminAsync();
        Guid infra = await CreateSectorAsAdminAsync();
        Guid requesterId = await InsertUserAsync("Maria", "Souza", UniqueEmail());

        Guid lateEvening = await InsertOwnTicketAsync(
            requesterId, helpdesk, TicketStatus.Open, "Impressora", new DateTime(2026, 11, 1, 2, 30, 0, DateTimeKind.Utc));
        Guid morning = await InsertOwnTicketAsync(
            requesterId, helpdesk, TicketStatus.Resolved, "Acesso ao e-mail", new DateTime(2026, 11, 1, 13, 0, 0, DateTimeKind.Utc));
        await SetResolvedAtAsync(morning, new DateTime(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc));
        Guid midMonth = await InsertOwnTicketAsync(
            requesterId, helpdesk, TicketStatus.Open, "Monitor", new DateTime(2026, 11, 15, 13, 0, 0, DateTimeKind.Utc));
        Guid inInfra = await InsertOwnTicketAsync(
            requesterId, infra, TicketStatus.Open, "Rede", new DateTime(2026, 11, 1, 13, 0, 0, DateTimeKind.Utc));

        return new PeriodScenario(helpdesk, infra, lateEvening, morning, midMonth, inInfra);
    }

    private async Task SetResolvedAtAsync(Guid ticketId, DateTime resolvedAtUtc)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Tickets
            .Where(t => t.Id == ticketId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.ResolvedAtUtc, resolvedAtUtc));
    }

    private sealed record PeriodScenario(
        Guid Helpdesk,
        Guid Infra,
        Guid LateEveningBefore,
        Guid MorningOfFirst,
        Guid MidMonth,
        Guid InInfra);

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

    /// <summary>
    /// Troca o status do chamado como administrador (que atende a fila e tem <c>tickets:cancel</c>) e volta ao token que
    /// estava configurado.
    /// </summary>
    private async Task UpdateStatusAsync(Guid ticketId, int status)
    {
        await RunAsAdministratorAsync(async () =>
        {
            HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
            {
                description = "The printer on the 3rd floor is not working",
                status
            });
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        });
    }

    private Guid GetTicketSectorId(Guid ticketId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return context.Tickets.Where(t => t.Id == ticketId).Select(t => t.SectorId).Single();
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
        sla.ShouldNotBeNull();

        // Todos os campos atuais do detalhe continuam, com os valores gravados.
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Ticket stored = await context.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId);
        string sectorName = await context.Sectors.Where(s => s.Id == stored.SectorId).Select(s => s.Name).SingleAsync();

        detail.Id.ShouldBe(ticketId);
        detail.Title.ShouldBe("Printer is broken");
        detail.Description.ShouldBe("The printer on the 3rd floor is not working");
        detail.Priority.ShouldBe((int)TicketPriority.Urgent);
        detail.Status.ShouldBe((int)TicketStatus.Open);
        detail.OpenedByUserId.ShouldBe(stored.CreatedByUserId);
        detail.OpenedByName.ShouldBe("Test User");
        detail.AssignedToUserId.ShouldBeNull();
        detail.AssignedToName.ShouldBeNull();
        detail.SectorId.ShouldBe(stored.SectorId);
        detail.SectorName.ShouldBe(sectorName);
        detail.CreatedAtUtc.ShouldBe(stored.CreatedAtUtc, TimeSpan.FromMilliseconds(1));
        detail.DueAtUtc.ShouldBe(stored.DueAtUtc, TimeSpan.FromMilliseconds(1));
        detail.SlaWarningAtUtc.ShouldBe(stored.SlaWarningAtUtc, TimeSpan.FromMilliseconds(1));
        detail.ResolvedAtUtc.ShouldBeNull();
        detail.SlaStatus.ShouldBe(1);
        detail.Sla.ShouldNotBeNull();

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
        (Guid ticketId, AccessTokens requesterTokens) = await CreateTicketAsRequesterAsync();
        Guid sectorId = GetTicketSectorId(ticketId);

        await AuthenticateAsAdminAsync();
        SlaSummaryDto? summaryBefore = await HttpClient.GetFromJsonAsync<SlaSummaryDto>($"tickets/sla-summary?sectorId={sectorId}");
        Authenticate(requesterTokens.AccessToken);

        // Act
        await UpdateStatusAsync(ticketId, 5); // Canceled

        // Assert
        await AuthenticateAsAdminAsync();
        SlaSummaryDto? summaryAfter = await HttpClient.GetFromJsonAsync<SlaSummaryDto>($"tickets/sla-summary?sectorId={sectorId}");
        summaryBefore.ShouldBe(new SlaSummaryDto(1, 1, 0, 0));
        summaryAfter.ShouldBe(new SlaSummaryDto(0, 0, 0, 0));
        Authenticate(requesterTokens.AccessToken);

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
    public async Task UpdateStatus_Should_ReturnNoContentAndConflict_WhenTwoStatusChangesRunAtOnce()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        AccessTokens administratorTokens = await LoginAsync(AdminEmail, AdminPassword);

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Gatilho temporário que segura por um segundo a mudança de status, com a linha bloqueada: a outra mudança lê
        // o mesmo chamado nesse intervalo e, ao gravar, encontra outra versão (conflito de concorrência).
        await context.Database.ExecuteSqlRawAsync(DelayTicketStatusChangeSql);

        HttpResponseMessage[] responses;

        try
        {
            // Act
            responses = await Task.WhenAll(
                SendStatusChangeAsAsync(administratorTokens, ticketId, 2), // InProgress
                SendStatusChangeAsAsync(administratorTokens, ticketId, 5)); // Canceled
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync(DropDelayTicketStatusChangeSql);
        }

        // Assert
        responses.Select(r => r.StatusCode).Order()
            .ShouldBe([HttpStatusCode.NoContent, HttpStatusCode.Conflict]);

        Ticket stored = await context.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId);
        List<TicketSlaTimer> timers = GetTimers(ticketId);

        if (stored.Status == TicketStatus.Canceled)
        {
            responses[1].StatusCode.ShouldBe(HttpStatusCode.NoContent);
            timers.ShouldAllBe(t => t.StoppedOnUtc != null && t.CompletedOnUtc == null);
        }
        else
        {
            stored.Status.ShouldBe(TicketStatus.InProgress);
            responses[0].StatusCode.ShouldBe(HttpStatusCode.NoContent);
            timers.ShouldAllBe(t => t.StoppedOnUtc == null && t.CompletedOnUtc == null);
        }
    }

    private async Task<HttpResponseMessage> SendStatusChangeAsAsync(AccessTokens tokens, Guid ticketId, int status)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"tickets/{ticketId}")
        {
            Content = JsonContent.Create(new { description = "The printer on the 3rd floor is not working", status })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return await HttpClient.SendAsync(request);
    }

    private const string DelayTicketStatusChangeSql =
        """
        CREATE OR REPLACE FUNCTION public.test_delay_ticket_status_change() RETURNS trigger AS $$
        BEGIN
            PERFORM pg_sleep(1);
            RETURN NEW;
        END;
        $$ LANGUAGE plpgsql;

        CREATE TRIGGER test_delay_ticket_status_change
            BEFORE UPDATE ON public.tickets
            FOR EACH ROW
            WHEN (OLD.status IS DISTINCT FROM NEW.status)
            EXECUTE FUNCTION public.test_delay_ticket_status_change();
        """;

    private const string DropDelayTicketStatusChangeSql =
        """
        DROP TRIGGER IF EXISTS test_delay_ticket_status_change ON public.tickets;
        DROP FUNCTION IF EXISTS public.test_delay_ticket_status_change();
        """;

    [Fact]
    public async Task Migration_Should_KeepSlaSummaryAndFilteredListing_ForExistingTickets()
    {
        // Arrange: chamados gravados como antes da mudança (sem plano nem cronômetros), um em cada situação do SLA,
        // num setor só deles. O plano fica opcional durante o teste e volta a ser obrigatório no fim.
        Guid sectorId = await CreateSectorAsAdminAsync();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid requesterId = await context.Users.Where(u => u.Email == AdminEmail).Select(u => u.Id).SingleAsync();
        DateTime nowUtc = DateTime.UtcNow;

        await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.tickets ALTER COLUMN sla_plan DROP NOT NULL;");

        try
        {
            // Urgente: 4 h de prazo e alerta em 3 h, contados da abertura.
            await InsertLegacyTicketAsync(context, Guid.NewGuid(), requesterId, sectorId, "Urgent", "Open", nowUtc.AddHours(-1), null);
            await InsertLegacyTicketAsync(context, Guid.NewGuid(), requesterId, sectorId, "Urgent", "Open", nowUtc.AddHours(-3.5), null);
            await InsertLegacyTicketAsync(context, Guid.NewGuid(), requesterId, sectorId, "Urgent", "InProgress", nowUtc.AddHours(-5), null);
            await InsertLegacyTicketAsync(context, Guid.NewGuid(), requesterId, sectorId, "Urgent", "Resolved", nowUtc.AddHours(-10), nowUtc.AddHours(-8));
            await InsertLegacyTicketAsync(context, Guid.NewGuid(), requesterId, sectorId, "Urgent", "Closed", nowUtc.AddHours(-10), nowUtc.AddHours(-5));
            await InsertLegacyTicketAsync(context, Guid.NewGuid(), requesterId, sectorId, "Urgent", "Canceled", nowUtc.AddHours(-1), null);

            await AuthenticateAsAdminAsync();
            (SlaSummaryDto Summary, Dictionary<int, List<Guid>> Listing) before = await GetSlaViewsAsync(sectorId);

            // Act
            for (int run = 0; run < 2; run++)
            {
                await context.Database.ExecuteSqlRawAsync(Add_Ticket_Sla_Timers.BackfillSlaPlansSql);
                await context.Database.ExecuteSqlRawAsync(Add_Ticket_Sla_Timers.BackfillResolutionTimersSql);
            }

            (SlaSummaryDto Summary, Dictionary<int, List<Guid>> Listing) after = await GetSlaViewsAsync(sectorId);

            // Assert: os mesmos números e os mesmos chamados em cada situação, que tem um chamado cada.
            before.Summary.ShouldBe(new SlaSummaryDto(3, 1, 1, 1));
            after.Summary.ShouldBe(before.Summary);
            before.Listing.Values.ShouldAllBe(ids => ids.Count == 1);

            foreach ((int slaStatus, List<Guid> ids) in before.Listing)
            {
                after.Listing[slaStatus].ShouldBe(ids);
            }
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync(Add_Ticket_Sla_Timers.BackfillSlaPlansSql);
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE public.tickets ALTER COLUMN sla_plan SET NOT NULL;");
        }
    }

    // O resumo e a listagem filtrada por cada situação do SLA (1 a 6), só do setor informado.
    private async Task<(SlaSummaryDto Summary, Dictionary<int, List<Guid>> Listing)> GetSlaViewsAsync(Guid sectorId)
    {
        SlaSummaryDto? summary = await HttpClient.GetFromJsonAsync<SlaSummaryDto>($"tickets/sla-summary?sectorId={sectorId}");
        var listing = new Dictionary<int, List<Guid>>();

        for (int slaStatus = 1; slaStatus <= 6; slaStatus++)
        {
            TicketIdsPageDto? page = await HttpClient.GetFromJsonAsync<TicketIdsPageDto>(
                $"tickets?sectorId={sectorId}&slaStatus={slaStatus}&pageSize=50");
            listing[slaStatus] = [.. page!.Items.Select(t => t.Id).Order()];
        }

        return (summary!, listing);
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
        // A conclusão dentro do prazo também tira o cronômetro da fila do monitoramento (spec 006), no mesmo registro.
        modified.ShouldAllBe(a => a.AffectedColumns == $"{nameof(TicketSlaTimer.CompletedOnUtc)},{nameof(TicketSlaTimer.NextActionOnUtc)}");
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
        string Description,
        int Priority,
        int Status,
        Guid OpenedByUserId,
        string OpenedByName,
        Guid? AssignedToUserId,
        string? AssignedToName,
        Guid SectorId,
        string SectorName,
        DateTime CreatedAtUtc,
        DateTime DueAtUtc,
        DateTime SlaWarningAtUtc,
        DateTime? ResolvedAtUtc,
        int SlaStatus,
        SlaDto Sla);

    private sealed record TicketIdDto(Guid Id);

    private sealed record TicketIdsPageDto(List<TicketIdDto> Items);

    // ----- Monitoramento do SLA (spec 006) -----

    private SlaBreachEventRecorder BreachEvents => Services.GetRequiredService<SlaBreachEventRecorder>();

    private SlaMonitorBackgroundService CreateSlaMonitor(int batchSize = 200, ILogger<SlaMonitorBackgroundService>? logger = null) =>
        new(
            logger ?? NullLogger<SlaMonitorBackgroundService>.Instance,
            Options.Create(new SlaMonitorSettings { BatchSize = batchSize, SleepTimeInMilliseconds = 1000 }),
            Services,
            Services.GetRequiredService<NpgsqlDataSource>());

    // Uma volta do monitoramento, no instante real.
    private async Task RunSlaMonitorAsync(int batchSize = 200, ILogger<SlaMonitorBackgroundService>? logger = null)
    {
        using SlaMonitorBackgroundService monitor = CreateSlaMonitor(batchSize, logger);

        await monitor.ProcessSlaBreachesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Leva para o passado os instantes dos cronômetros do chamado (início, alerta, prazo e próxima ação), como se ele
    /// tivesse sido aberto antes: o cenário do teste fica vencido no instante real, sem mexer no relógio da aplicação.
    /// </summary>
    private async Task ShiftSlaTimersAsync(Guid ticketId, TimeSpan shift)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.TicketSlaTimers
            .Where(t => t.TicketId == ticketId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.StartedAtUtc, t => t.StartedAtUtc - shift)
                .SetProperty(t => t.WarningOnUtc, t => t.WarningOnUtc - shift)
                .SetProperty(t => t.DueOnUtc, t => t.DueOnUtc - shift)
                .SetProperty(t => t.NextActionOnUtc, t => t.NextActionOnUtc - shift));
    }

    // A primeira resposta vence `overdue` antes de agora; a resolução continua no prazo.
    private async Task MakeFirstResponseOverdueAsync(Guid ticketId, TimeSpan overdue)
    {
        DateTime firstResponseDue = GetTimers(ticketId).Single(t => t.Metric == SlaMetric.FirstResponse).DueOnUtc;

        await ShiftSlaTimersAsync(ticketId, firstResponseDue - DateTime.UtcNow + overdue);
    }

    // Os dois cronômetros vencem antes de agora.
    private async Task MakeAllSlaTimersOverdueAsync(Guid ticketId, TimeSpan overdue)
    {
        DateTime lastDue = GetTimers(ticketId).Max(t => t.DueOnUtc);

        await ShiftSlaTimersAsync(ticketId, lastDue - DateTime.UtcNow + overdue);
    }

    // Processa o que estiver vencido no banco compartilhado, para os testes que contam lotes partirem de uma fila vazia.
    private async Task DrainSlaQueueAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        for (int round = 0; round < 50; round++)
        {
            DateTime nowUtc = DateTime.UtcNow;

            if (!await context.TicketSlaTimers.AnyAsync(t => t.NextActionOnUtc != null && t.NextActionOnUtc <= nowUtc))
            {
                return;
            }

            await RunSlaMonitorAsync(batchSize: 1000);
        }
    }

    [Fact]
    public async Task Create_Should_QueueSlaTimersAtDeadline_WithFilteredIndex()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        List<string> columns = await context.Database
            .SqlQueryRaw<string>("SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = 'ticket_sla_timers'")
            .ToListAsync();
        string? indexDefinition = await context.Database
            .SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = 'ix_ticket_sla_timers_next_action_on_utc'")
            .SingleOrDefaultAsync();

        // Assert
        columns.ShouldContain("breached_on_utc");
        columns.ShouldContain("next_action_on_utc");
        indexDefinition.ShouldNotBeNull();
        indexDefinition.ShouldContain("WHERE (next_action_on_utc IS NOT NULL)");

        List<TicketSlaTimer> timers = GetTimers(ticketId);
        timers.Count.ShouldBe(2);
        timers.ShouldAllBe(t => t.NextActionOnUtc == t.DueOnUtc && t.BreachedOnUtc == null);
    }

    [Fact]
    public async Task SlaMonitor_Should_BreachOnlyOverdueFirstResponse_OnceWithEventAndAudit()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();

        // Act: antes do prazo, nada muda.
        await RunSlaMonitorAsync();
        List<TicketSlaTimer> beforeDue = GetTimers(ticketId);

        // Depois do prazo da primeira resposta.
        await MakeFirstResponseOverdueAsync(ticketId, TimeSpan.FromMinutes(1));
        await RunSlaMonitorAsync();

        // Assert
        beforeDue.ShouldAllBe(t => t.BreachedOnUtc == null && t.NextActionOnUtc == t.DueOnUtc);

        List<TicketSlaTimer> timers = GetTimers(ticketId);
        TicketSlaTimer firstResponse = timers.Single(t => t.Metric == SlaMetric.FirstResponse);
        firstResponse.BreachedOnUtc.ShouldBe(firstResponse.DueOnUtc);
        firstResponse.NextActionOnUtc.ShouldBeNull();

        TicketSlaTimer resolution = timers.Single(t => t.Metric == SlaMetric.Resolution);
        resolution.BreachedOnUtc.ShouldBeNull();
        resolution.NextActionOnUtc.ShouldBe(resolution.DueOnUtc);

        TicketSlaBreachedDomainEvent breach = BreachEvents.For(ticketId).ShouldHaveSingleItem();
        breach.ShouldBe(new TicketSlaBreachedDomainEvent(ticketId, SlaMetric.FirstResponse, 1, firstResponse.DueOnUtc));

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        AuditLogEntry audit = (await context.AuditLogs
            .Where(a => a.EntityName == nameof(TicketSlaTimer) && a.EntityId == firstResponse.Id.ToString() && a.Action == "Modified")
            .ToListAsync()).ShouldHaveSingleItem();
        audit.UserId.ShouldBeNull();
        audit.AffectedColumns!.Split(',').Order().ShouldBe([nameof(TicketSlaTimer.BreachedOnUtc), nameof(TicketSlaTimer.NextActionOnUtc)]);
    }

    [Fact]
    public async Task SlaMonitor_Should_BreachBothTimersInDeadlineOrder_AndNothingMoreOnNextRun()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        await MakeAllSlaTimersOverdueAsync(ticketId, TimeSpan.FromMinutes(5));

        // Act
        await RunSlaMonitorAsync();
        List<TicketSlaTimer> afterFirstRun = GetTimers(ticketId);
        await RunSlaMonitorAsync();

        // Assert
        afterFirstRun.ShouldAllBe(t => t.BreachedOnUtc == t.DueOnUtc && t.NextActionOnUtc == null);

        List<TicketSlaBreachedDomainEvent> events = BreachEvents.For(ticketId);
        events.Select(e => e.Metric).ShouldBe([SlaMetric.FirstResponse, SlaMetric.Resolution]);
        events.Select(e => e.DueOnUtc).ShouldBe(afterFirstRun.OrderBy(t => t.Metric).Select(t => t.DueOnUtc));

        GetTimers(ticketId).Select(t => (t.BreachedOnUtc, t.NextActionOnUtc))
            .ShouldBe(afterFirstRun.Select(t => (t.BreachedOnUtc, t.NextActionOnUtc)));
    }

    [Fact]
    public async Task SlaMonitor_Should_ProcessOneBatchPerRun_InNextActionOrder()
    {
        // Arrange: 7 primeiras respostas vencidas, a mais antiga primeiro.
        await DrainSlaQueueAsync();

        var ticketIds = new List<Guid>();

        for (int index = 0; index < 7; index++)
        {
            (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
            // Minutos, e não horas: o prazo da resolução fica pelo menos 210 minutos depois e continua no futuro.
            await MakeFirstResponseOverdueAsync(ticketId, TimeSpan.FromMinutes(8 - index));
            ticketIds.Add(ticketId);
        }

        // Act & Assert: lotes de 3.
        await RunSlaMonitorAsync(batchSize: 3);
        BreachedFirstResponses(ticketIds).ShouldBe(ticketIds.Take(3));

        await RunSlaMonitorAsync(batchSize: 3);
        BreachedFirstResponses(ticketIds).ShouldBe(ticketIds.Take(6));

        await RunSlaMonitorAsync(batchSize: 3);
        BreachedFirstResponses(ticketIds).ShouldBe(ticketIds);

        BreachEvents.For(ticketIds).Count.ShouldBe(7);
    }

    private List<Guid> BreachedFirstResponses(List<Guid> ticketIds) =>
        [.. ticketIds.Where(id => GetTimers(id).Single(t => t.Metric == SlaMetric.FirstResponse).BreachedOnUtc != null)];

    [Fact]
    public async Task SlaMonitor_Should_BreachFirstResponseAnsweredLate_OnNextRunOnly()
    {
        // Arrange
        (Guid ticketId, AccessTokens requesterTokens) = await CreateTicketAsRequesterAsync();
        AccessTokens agentTokens = await CreateAgentAsync();
        await MakeFirstResponseOverdueAsync(ticketId, TimeSpan.FromMinutes(5));

        // Act: o Atendente responde depois do prazo, antes de o monitoramento passar.
        (await SendMessageAsAsync(agentTokens, ticketId, "Sorry for the delay.")).StatusCode.ShouldBe(HttpStatusCode.OK);
        TicketSlaTimer answered = GetTimers(ticketId).Single(t => t.Metric == SlaMetric.FirstResponse);
        Authenticate(requesterTokens.AccessToken);
        int stateAfterAnswer = (await GetSlaAsync(ticketId)).Objectives.Single(o => o.Metric == FirstResponse).State;
        int eventsAfterAnswer = BreachEvents.For(ticketId).Count;

        await RunSlaMonitorAsync();
        TicketSlaTimer afterFirstRun = GetTimers(ticketId).Single(t => t.Metric == SlaMetric.FirstResponse);
        await RunSlaMonitorAsync();

        // Assert
        stateAfterAnswer.ShouldBe(5); // Missed
        answered.CompletedOnUtc.ShouldNotBeNull();
        answered.BreachedOnUtc.ShouldBeNull();
        answered.NextActionOnUtc.ShouldBe(answered.DueOnUtc);
        eventsAfterAnswer.ShouldBe(0);

        afterFirstRun.BreachedOnUtc.ShouldBe(answered.DueOnUtc);
        afterFirstRun.NextActionOnUtc.ShouldBeNull();
        afterFirstRun.CompletedOnUtc.ShouldBe(answered.CompletedOnUtc);
        BreachEvents.For(ticketId).ShouldHaveSingleItem().Metric.ShouldBe(SlaMetric.FirstResponse);
    }

    [Fact]
    public async Task SlaMonitor_Should_NotBreachCanceledTicket_EvenWhenOverdue()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        await MakeAllSlaTimersOverdueAsync(ticketId, TimeSpan.FromMinutes(5));

        // Act
        await UpdateStatusAsync(ticketId, 5); // Canceled
        await RunSlaMonitorAsync();

        // Assert
        GetTimers(ticketId).ShouldAllBe(t => t.StoppedOnUtc != null && t.BreachedOnUtc == null && t.NextActionOnUtc == null);
        BreachEvents.For(ticketId).ShouldBeEmpty();
    }

    [Fact]
    public async Task SlaMonitor_Should_LeaveTimersAlone_WhenAnsweredAndResolvedOnTime_AndQueueReopenedCycle()
    {
        // Arrange
        (Guid ticketId, AccessTokens requesterTokens) = await CreateTicketAsRequesterAsync();
        AccessTokens agentTokens = await CreateAgentAsync();

        // Act: resposta e resolução antes dos prazos.
        (await SendMessageAsAsync(agentTokens, ticketId, "On my way.")).StatusCode.ShouldBe(HttpStatusCode.OK);
        TicketSlaTimer afterAnswer = GetTimers(ticketId).Single(t => t.Metric == SlaMetric.FirstResponse);

        Authenticate(requesterTokens.AccessToken);
        await UpdateStatusAsync(ticketId, 2); // InProgress
        await UpdateStatusAsync(ticketId, 3); // Resolved
        TicketSlaTimer afterResolve = GetTimers(ticketId).Single(t => t.Metric == SlaMetric.Resolution);

        await RunSlaMonitorAsync();
        List<TicketSlaTimer> afterRun = GetTimers(ticketId);

        (await HttpClient.PostAsync($"tickets/{ticketId}/reopen", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Assert
        afterAnswer.NextActionOnUtc.ShouldBeNull();
        afterResolve.NextActionOnUtc.ShouldBeNull();
        afterRun.ShouldAllBe(t => t.NextActionOnUtc == null && t.BreachedOnUtc == null);
        BreachEvents.For(ticketId).ShouldBeEmpty();

        List<TicketSlaTimer> timers = GetTimers(ticketId);
        timers.Single(t => t.Metric == SlaMetric.Resolution && t.Cycle == 1).NextActionOnUtc.ShouldBeNull();
        TicketSlaTimer secondCycle = timers.Single(t => t.Metric == SlaMetric.Resolution && t.Cycle == 2);
        secondCycle.NextActionOnUtc.ShouldBe(secondCycle.DueOnUtc);
    }

    [Fact]
    public async Task PostMessage_Should_CompleteFirstResponse_WhenMonitorBreachesItBeforeTheHandlerSaves()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        AccessTokens agentTokens = await CreateAgentAsync();
        await MakeFirstResponseOverdueAsync(ticketId, TimeSpan.FromMinutes(2));

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Gatilho temporário que segura a gravação da mensagem: o handler já leu a primeira resposta em aberto, e o
        // monitoramento grava a violação antes de o handler salvar a conclusão.
        await context.Database.ExecuteSqlRawAsync(DelayInsertSql("ticket_messages"));

        HttpResponseMessage response;

        try
        {
            // Act
            Task<HttpResponseMessage> post = SendMessageAsAsync(agentTokens, ticketId, "Sorry for the delay.");
            await Task.Delay(TimeSpan.FromSeconds(1));
            await RunSlaMonitorAsync();
            response = await post;
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync(DropDelayInsertSql("ticket_messages"));
        }

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Guid messageId = await response.Content.ReadFromJsonAsync<Guid>();
        DateTime messageAtUtc = await context.TicketMessages.Where(m => m.Id == messageId).Select(m => m.CreatedAtUtc).SingleAsync();

        TicketSlaTimer firstResponse = GetTimers(ticketId).Single(t => t.Metric == SlaMetric.FirstResponse);
        firstResponse.CompletedOnUtc.ShouldBe(messageAtUtc);
        firstResponse.BreachedOnUtc.ShouldBe(firstResponse.DueOnUtc);
        BreachEvents.For(ticketId).ShouldHaveSingleItem().Metric.ShouldBe(SlaMetric.FirstResponse);
    }

    [Fact]
    public async Task Cancel_Should_StopTimersAndKeepBreach_WhenMonitorBreachesThemBeforeTheHandlerSaves()
    {
        // Arrange
        (Guid ticketId, AccessTokens requesterTokens) = await CreateTicketAsRequesterAsync();
        await MakeAllSlaTimersOverdueAsync(ticketId, TimeSpan.FromMinutes(2));

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // O histórico da mudança de status é gravado no mesmo SaveChanges: segurar a inserção dele deixa o
        // monitoramento gravar a violação depois de o handler ler os cronômetros e antes de salvar a parada.
        await context.Database.ExecuteSqlRawAsync(DelayInsertSql("ticket_histories"));

        HttpResponseMessage response;

        try
        {
            // Act
            Task<HttpResponseMessage> cancel = SendStatusChangeAsAsync(requesterTokens, ticketId, 5); // Canceled
            await Task.Delay(TimeSpan.FromSeconds(1));
            await RunSlaMonitorAsync();
            response = await cancel;
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync(DropDelayInsertSql("ticket_histories"));
        }

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        GetTimers(ticketId).ShouldAllBe(t => t.StoppedOnUtc != null && t.BreachedOnUtc == t.DueOnUtc && t.NextActionOnUtc == null);
        BreachEvents.For(ticketId).Count.ShouldBe(2);
    }

    private static string DelayInsertSql(string table) =>
        $"""
        CREATE OR REPLACE FUNCTION public.test_delay_insert() RETURNS trigger AS $$
        BEGIN
            PERFORM pg_sleep(3);
            RETURN NEW;
        END;
        $$ LANGUAGE plpgsql;

        CREATE TRIGGER test_delay_insert BEFORE INSERT ON public.{table}
            FOR EACH ROW EXECUTE FUNCTION public.test_delay_insert();
        """;

    private static string DropDelayInsertSql(string table) =>
        $"""
        DROP TRIGGER IF EXISTS test_delay_insert ON public.{table};
        DROP FUNCTION IF EXISTS public.test_delay_insert();
        """;

    [Fact]
    public async Task SlaMonitor_Should_RollBackBatchAndLogCritical_WhenSaveConflicts_AndProcessItOnNextRun()
    {
        // Arrange
        await DrainSlaQueueAsync();
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        await MakeFirstResponseOverdueAsync(ticketId, TimeSpan.FromMinutes(1));

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Gatilho temporário que descarta a primeira gravação de violação: o UPDATE não altera nenhuma linha, e o EF
        // trata como conflito de concorrência (DbUpdateConcurrencyException).
        await context.Database.ExecuteSqlRawAsync(SkipFirstBreachUpdateSql);
        var logger = new TestLogger<SlaMonitorBackgroundService>();

        try
        {
            // Act
            await RunSlaMonitorAsync(logger: logger);
            TicketSlaTimer afterFailure = GetTimers(ticketId).Single(t => t.Metric == SlaMetric.FirstResponse);
            int eventsAfterFailure = BreachEvents.For(ticketId).Count;

            await RunSlaMonitorAsync(logger: logger);

            // Assert
            afterFailure.BreachedOnUtc.ShouldBeNull();
            afterFailure.NextActionOnUtc.ShouldBe(afterFailure.DueOnUtc);
            eventsAfterFailure.ShouldBe(0);

            TestLogEntry critical = logger.Entries.Where(e => e.Level == LogLevel.Critical).ShouldHaveSingleItem();
            critical.Exception.ShouldBeOfType<DbUpdateConcurrencyException>();

            TicketSlaTimer afterRetry = GetTimers(ticketId).Single(t => t.Metric == SlaMetric.FirstResponse);
            afterRetry.BreachedOnUtc.ShouldBe(afterRetry.DueOnUtc);
            BreachEvents.For(ticketId).ShouldHaveSingleItem();
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync(DropSkipFirstBreachUpdateSql);
        }
    }

    private const string SkipFirstBreachUpdateSql =
        """
        CREATE SEQUENCE public.test_breach_updates START 1;

        CREATE OR REPLACE FUNCTION public.test_skip_first_breach_update() RETURNS trigger AS $$
        BEGIN
            IF nextval('public.test_breach_updates') = 1 THEN
                RETURN NULL;
            END IF;
            RETURN NEW;
        END;
        $$ LANGUAGE plpgsql;

        CREATE TRIGGER test_skip_first_breach_update BEFORE UPDATE ON public.ticket_sla_timers
            FOR EACH ROW
            WHEN (OLD.breached_on_utc IS NULL AND NEW.breached_on_utc IS NOT NULL)
            EXECUTE FUNCTION public.test_skip_first_breach_update();
        """;

    private const string DropSkipFirstBreachUpdateSql =
        """
        DROP TRIGGER IF EXISTS test_skip_first_breach_update ON public.ticket_sla_timers;
        DROP FUNCTION IF EXISTS public.test_skip_first_breach_update();
        DROP SEQUENCE IF EXISTS public.test_breach_updates;
        """;

    [Fact]
    public async Task Migration_Should_QueueOpenLegacyTimersAndBreachLateOnes_WithoutDuplicating()
    {
        // Arrange: cronômetros como estavam antes da migration da spec 006 (sem violação nem próxima ação).
        Guid sectorId = await CreateSectorAsAdminAsync();
        DateTime nowUtc = DateTime.UtcNow;

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid requesterId = await context.Users.Where(u => u.Email == AdminEmail).Select(u => u.Id).SingleAsync();

        Guid openOverdue = AddLegacyTicket(context, requesterId, sectorId, nowUtc.AddHours(-5), TicketStatus.Open);
        Guid openOnTime = AddLegacyTicket(context, requesterId, sectorId, nowUtc.AddHours(-1), TicketStatus.Open);
        Guid resolvedOnTime = AddLegacyTicket(context, requesterId, sectorId, nowUtc.AddHours(-10), TicketStatus.Resolved, completedAfter: TimeSpan.FromHours(2));
        Guid resolvedLate = AddLegacyTicket(context, requesterId, sectorId, nowUtc.AddHours(-10), TicketStatus.Resolved, completedAfter: TimeSpan.FromHours(5));
        Guid canceled = AddLegacyTicket(context, requesterId, sectorId, nowUtc.AddHours(-5), TicketStatus.Canceled, stoppedAfter: TimeSpan.FromHours(1));
        Guid deleted = AddLegacyTicket(context, requesterId, sectorId, nowUtc.AddHours(-5), TicketStatus.Open, deletedAtUtc: nowUtc.AddHours(-4));
        await context.SaveChangesAsync();

        Guid[] ticketIds = [openOverdue, openOnTime, resolvedOnTime, resolvedLate, canceled, deleted];

        // Act: o preenchimento roda duas vezes, e depois uma volta do monitoramento.
        await context.Database.ExecuteSqlRawAsync(Add_Sla_Breach_Monitoring.BackfillNextActionSql);
        await context.Database.ExecuteSqlRawAsync(Add_Sla_Breach_Monitoring.BackfillBreachedSql);
        List<TicketSlaTimer> afterFirstBackfill = LegacyTimers(ticketIds);

        await context.Database.ExecuteSqlRawAsync(Add_Sla_Breach_Monitoring.BackfillNextActionSql);
        await context.Database.ExecuteSqlRawAsync(Add_Sla_Breach_Monitoring.BackfillBreachedSql);
        List<TicketSlaTimer> afterSecondBackfill = LegacyTimers(ticketIds);

        await RunSlaMonitorAsync();

        // Assert
        TicketSlaTimer Timer(Guid id) => afterFirstBackfill.Single(t => t.TicketId == id);

        Timer(openOverdue).NextActionOnUtc.ShouldBe(Timer(openOverdue).DueOnUtc);
        Timer(openOnTime).NextActionOnUtc.ShouldBe(Timer(openOnTime).DueOnUtc);
        Timer(resolvedLate).BreachedOnUtc.ShouldBe(Timer(resolvedLate).DueOnUtc);
        Timer(resolvedLate).NextActionOnUtc.ShouldBeNull();

        foreach (Guid id in new[] { resolvedOnTime, canceled, deleted })
        {
            Timer(id).NextActionOnUtc.ShouldBeNull();
            Timer(id).BreachedOnUtc.ShouldBeNull();
        }

        afterSecondBackfill.Select(t => (t.TicketId, t.BreachedOnUtc, t.NextActionOnUtc))
            .ShouldBe(afterFirstBackfill.Select(t => (t.TicketId, t.BreachedOnUtc, t.NextActionOnUtc)));

        List<TicketSlaTimer> afterRun = LegacyTimers(ticketIds);
        afterRun.Single(t => t.TicketId == openOverdue).BreachedOnUtc.ShouldBe(Timer(openOverdue).DueOnUtc);
        afterRun.Single(t => t.TicketId == openOnTime).BreachedOnUtc.ShouldBeNull();

        BreachEvents.For(ticketIds).ShouldHaveSingleItem().TicketId.ShouldBe(openOverdue);
    }

    private List<TicketSlaTimer> LegacyTimers(Guid[] ticketIds)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return [.. context.TicketSlaTimers.AsNoTracking().Where(t => ticketIds.Contains(t.TicketId)).OrderBy(t => t.TicketId)];
    }

    // Chamado Urgente no plano legado (4 h corridas), com o cronômetro de resolução como a migration da spec 005 grava.
    private static Guid AddLegacyTicket(
        ApplicationDbContext context,
        Guid requesterId,
        Guid sectorId,
        DateTime createdAtUtc,
        TicketStatus status,
        TimeSpan? completedAfter = null,
        TimeSpan? stoppedAfter = null,
        DateTime? deletedAtUtc = null)
    {
        var ticketId = Guid.NewGuid();
        DateTime dueAtUtc = createdAtUtc.AddHours(4);
        DateTime warningAtUtc = createdAtUtc.AddHours(3);
        DateTime? completedAtUtc = createdAtUtc + completedAfter;

        context.Tickets.Add(new Ticket
        {
            Id = ticketId,
            Title = "Legacy ticket",
            Description = "Opened before the SLA monitoring",
            Priority = TicketPriority.Urgent,
            Status = status,
            CreatedByUserId = requesterId,
            SectorId = sectorId,
            CreatedAtUtc = createdAtUtc,
            DueAtUtc = dueAtUtc,
            SlaWarningAtUtc = warningAtUtc,
            ResolvedAtUtc = completedAtUtc,
            DeletedAtUtc = deletedAtUtc,
            SlaPlan = TicketSla.LegacyPlan(TicketPriority.Urgent)
        });

        context.TicketSlaTimers.Add(new TicketSlaTimer
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Metric = SlaMetric.Resolution,
            Cycle = 1,
            TargetMinutes = 240,
            CalendarId = SlaCalendarIds.Continuous,
            StartedAtUtc = createdAtUtc,
            WarningOnUtc = warningAtUtc,
            DueOnUtc = dueAtUtc,
            CompletedOnUtc = completedAtUtc,
            StoppedOnUtc = createdAtUtc + stoppedAfter
        });

        return ticketId;
    }

    [Fact]
    public async Task SlaMonitor_Should_KeepSlaReadings_BeforeAndAfterBreachIsRecorded()
    {
        // Arrange
        (Guid ticketId, AccessTokens requesterTokens) = await CreateTicketAsRequesterAsync();
        Guid sectorId = GetTicketSectorId(ticketId);
        await MakeFirstResponseOverdueAsync(ticketId, TimeSpan.FromMinutes(1));

        // Act
        (SlaDto Sla, SlaSummaryDto Summary, Dictionary<int, List<Guid>> Listing) before = await ReadSlaViewsAsync(ticketId, sectorId, requesterTokens);
        await RunSlaMonitorAsync();
        (SlaDto Sla, SlaSummaryDto Summary, Dictionary<int, List<Guid>> Listing) after = await ReadSlaViewsAsync(ticketId, sectorId, requesterTokens);

        // Assert: a violação gravada não muda nenhuma leitura.
        GetTimers(ticketId).Single(t => t.Metric == SlaMetric.FirstResponse).BreachedOnUtc.ShouldNotBeNull();

        after.Sla.Plan.ShouldBe(before.Sla.Plan);
        after.Sla.Objectives.Select(o => (o.Metric, o.Cycle, o.State, o.DueAtUtc, o.CompletedAtUtc, o.StoppedAtUtc))
            .ShouldBe(before.Sla.Objectives.Select(o => (o.Metric, o.Cycle, o.State, o.DueAtUtc, o.CompletedAtUtc, o.StoppedAtUtc)));
        after.Summary.ShouldBe(before.Summary);

        foreach ((int slaStatus, List<Guid> ids) in before.Listing)
        {
            after.Listing[slaStatus].ShouldBe(ids);
        }
    }

    private async Task<(SlaDto Sla, SlaSummaryDto Summary, Dictionary<int, List<Guid>> Listing)> ReadSlaViewsAsync(
        Guid ticketId,
        Guid sectorId,
        AccessTokens requesterTokens)
    {
        Authenticate(requesterTokens.AccessToken);
        SlaDto sla = await GetSlaAsync(ticketId);

        await AuthenticateAsAdminAsync();
        (SlaSummaryDto summary, Dictionary<int, List<Guid>> listing) = await GetSlaViewsAsync(sectorId);

        return (sla, summary, listing);
    }

    [Fact]
    public async Task SlaMonitor_Should_IgnoreDeletedTicket()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
        await AuthenticateAsAdminAsync();

        // Act
        (await HttpClient.DeleteAsync($"tickets/{ticketId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        List<TicketSlaTimer> afterDelete = GetTimers(ticketId);
        await MakeAllSlaTimersOverdueAsync(ticketId, TimeSpan.FromMinutes(5));
        await RunSlaMonitorAsync();

        // Assert
        afterDelete.ShouldAllBe(t => t.NextActionOnUtc == null && t.BreachedOnUtc == null && t.StoppedOnUtc == null);
        GetTimers(ticketId).ShouldAllBe(t => t.NextActionOnUtc == null && t.BreachedOnUtc == null);
        BreachEvents.For(ticketId).ShouldBeEmpty();
    }

    [Fact]
    public async Task SlaMonitor_Should_BreachEachTimerOnce_WhenTwoInstancesRunAtOnce()
    {
        // Arrange: 3 chamados com os dois cronômetros vencidos.
        var ticketIds = new List<Guid>();

        for (int index = 0; index < 3; index++)
        {
            (Guid ticketId, _) = await CreateTicketAsRequesterAsync();
            await MakeAllSlaTimersOverdueAsync(ticketId, TimeSpan.FromMinutes(5));
            ticketIds.Add(ticketId);
        }

        // Act: com a trava nas mãos de outra instância, a volta não faz nada.
        await using (NpgsqlConnection otherInstance = await Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync())
        {
            await using var lockCommand = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", otherInstance);
            lockCommand.Parameters.AddWithValue("key", SlaMonitorBackgroundService.AdvisoryLockKey);
            await lockCommand.ExecuteScalarAsync();

            await RunSlaMonitorAsync();
        }

        List<TicketSlaTimer> whileLocked = [.. ticketIds.SelectMany(GetTimers)];

        // Duas voltas ao mesmo tempo, em escopos e conexões diferentes.
        await Task.WhenAll(RunSlaMonitorAsync(), RunSlaMonitorAsync());

        // Assert
        whileLocked.ShouldAllBe(t => t.BreachedOnUtc == null);

        List<TicketSlaTimer> timers = [.. ticketIds.SelectMany(GetTimers)];
        timers.Count.ShouldBe(6);
        timers.ShouldAllBe(t => t.BreachedOnUtc == t.DueOnUtc && t.NextActionOnUtc == null);

        List<TicketSlaBreachedDomainEvent> events = BreachEvents.For(ticketIds);
        events.Count.ShouldBe(6);
        events.Select(e => (e.TicketId, e.Metric)).Distinct().Count().ShouldBe(6);
    }

    [Fact]
    public async Task Assign_Should_LetAgentAssignOnlyToSelf_WhenAgentLacksAssignPermission()
    {
        // Arrange: Beto e Ana com a role Atendente, que não tem tickets:assign
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);
        Guid anaId = GetUserId(await CreateAgentAsync());
        AccessTokens betoTokens = await CreateAgentAsync();
        Guid betoId = GetUserId(betoTokens);
        Authenticate(betoTokens.AccessToken);

        // Act
        HttpResponseMessage toOther = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}/assignee", new { assignedToUserId = anaId });
        Guid? assigneeAfterOther = GetAssignee(ticketId);
        HttpResponseMessage toSelf = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}/assignee", new { assignedToUserId = betoId });

        // Assert
        toOther.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ProblemDto? problem = await toOther.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Tickets.CannotAssignToOthers");
        problem.Detail.ShouldBe("Você só pode atribuir o chamado a você mesmo.");
        assigneeAfterOther.ShouldBeNull();

        toSelf.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        GetAssignee(ticketId).ShouldBe(betoId);
    }

    [Fact]
    public async Task Migration_Should_GrantAdministrativeAssignPermissionOnlyToAdministrator_AndRunAgain()
    {
        // Arrange
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act: o vínculo já existe, então a migration roda de novo sem erro e sem duplicar
        await context.Database.ExecuteSqlRawAsync(
            Add_User_Sector_And_Tickets_Assign_Permission.InsertTicketsAssignPermissionSql);

        // Assert
        Domain.Roles.Permission permission = await context.Permissions.AsNoTracking()
            .SingleAsync(p => p.Code == "tickets:assign");
        permission.IsAdministrative.ShouldBeTrue();

        List<string> roles = await context.RolePermissions.AsNoTracking()
            .Where(rp => rp.PermissionCode == "tickets:assign")
            .Select(rp => rp.RoleName)
            .ToListAsync();
        roles.ShouldContain(RoleNames.Administrator);
        roles.Count(role => role == RoleNames.Administrator).ShouldBe(1);
        roles.ShouldNotContain(RoleNames.Agent);
        roles.ShouldNotContain(RoleNames.Collaborator);
    }

    [Fact]
    public async Task Assign_Should_ValidateAssigneeInOrder_WhenAdministratorAssigns()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid carlaId, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);
        Guid ticketId = await CreateTicketAsync(sectorId);

        Guid brunoId = GetUserId(await CreateAgentAsync());
        (await HttpClient.PutAsync($"users/{brunoId}/deactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        Guid adminId = GetUserId(adminTokens);

        // Act
        HttpResponseMessage collaborator = await SendAssignAsync(ticketId, carlaId);
        HttpResponseMessage inactive = await SendAssignAsync(ticketId, brunoId);
        HttpResponseMessage missing = await SendAssignAsync(ticketId, Guid.NewGuid());
        HttpResponseMessage self = await SendAssignAsync(ticketId, adminId);

        // Assert
        collaborator.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await collaborator.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Tickets.AssigneeNotAttendant");
        inactive.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await inactive.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Users.NotFound");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await missing.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Users.NotFound");
        self.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        GetAssignee(ticketId).ShouldBe(adminId);
    }

    [Fact]
    public async Task Assign_Should_ReturnNotActiveAndRecordNothing_WhenTicketIsResolved()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        Guid anaId = GetUserId(await CreateAgentAsync());
        Guid ticketId = await CreateTicketAsync(sectorId);
        await ResolveTicketAsync(ticketId);
        int historyCount = GetHistoryAfterCreation(ticketId).Count;

        // Act
        HttpResponseMessage response = await SendAssignAsync(ticketId, anaId);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Tickets.NotActive");
        GetAssignee(ticketId).ShouldBeNull();
        GetHistoryAfterCreation(ticketId).Count.ShouldBe(historyCount);
    }

    [Fact]
    public async Task Assign_Should_RecordAssignedThenReassigned_AndShowNamesInHistoryInPageQuery()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid anaId, string anaName) = await CreateNamedAgentAsync("Ana", "Souza");
        (Guid betoId, string betoName) = await CreateNamedAgentAsync("Beto", "Lima");
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        Guid adminId = GetUserId(adminTokens);
        Guid ticketId = await CreateTicketAsync(sectorId);

        // Act
        HttpResponseMessage toAna = await SendAssignAsync(ticketId, anaId);
        HttpResponseMessage toBeto = await SendAssignAsync(ticketId, betoId);

        // Assert: os fatos gravados
        toAna.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        toBeto.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var history = GetHistoryAfterCreation(ticketId).OrderBy(h => h.OccurredAtUtc).ToList();
        history.Count.ShouldBe(2);
        history[0].Action.ShouldBe(TicketHistoryAction.Assigned);
        history[0].OldValue.ShouldBeNull();
        history[0].NewValue.ShouldBe(anaId.ToString());
        history[0].ChangedByUserId.ShouldBe(adminId);
        history[0].OccurredAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        history[1].Action.ShouldBe(TicketHistoryAction.Reassigned);
        history[1].OldValue.ShouldBe(anaId.ToString());
        history[1].NewValue.ShouldBe(betoId.ToString());
        history[1].ChangedByUserId.ShouldBe(adminId);

        // Arrange: Ana é desativada e o responsável é removido
        (await HttpClient.PutAsync($"users/{anaId}/deactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await HttpClient.DeleteAsync($"tickets/{ticketId}/assignee")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act: a página do histórico, contando os comandos que leem nomes de usuário
        HttpResponseMessage response;
        int nameQueries;
        int pageQueriesWithNames;

        using (var nameCounter = new EfCommandCounter(sql => sql.Contains("first_name", StringComparison.Ordinal)))
        using (var pageCounter = new EfCommandCounter(sql =>
            sql.Contains("ticket_histories", StringComparison.Ordinal) &&
            sql.Contains("first_name", StringComparison.Ordinal)))
        {
            response = await HttpClient.GetAsync($"tickets/{ticketId}/history");
            nameQueries = nameCounter.Count;
            pageQueriesWithNames = pageCounter.Count;
        }

        // Assert: as descrições com os nomes, que vêm na própria consulta da página
        response.EnsureSuccessStatusCode();
        var descriptions = (await response.Content.ReadFromJsonAsync<CursorPageDto<TicketHistoryDto>>())!.Items
            .Select(h => h.Description)
            .ToList();
        descriptions.ShouldContain($"Responsável definido: {anaName}");
        descriptions.ShouldContain($"Responsável alterado de {anaName} para {betoName}");
        descriptions.ShouldContain($"Responsável removido: {betoName}");
        nameQueries.ShouldBe(1);
        pageQueriesWithNames.ShouldBe(1);
    }

    [Fact]
    public async Task Assign_Should_RecordNothing_WhenAssigneeIsAlreadyTheCurrentOne()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        Guid anaId = GetUserId(await CreateAgentAsync());
        Guid ticketId = await CreateTicketAsync(sectorId);
        (await SendAssignAsync(ticketId, anaId)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await SendAssignAsync(ticketId, anaId);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        GetHistoryAfterCreation(ticketId).ShouldHaveSingleItem().Action.ShouldBe(TicketHistoryAction.Assigned);
    }

    [Fact]
    public async Task Assign_Should_ReturnNoContentAndConflict_WhenTwoAssignmentsRunAtOnce()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();
        Guid anaId = GetUserId(await CreateAgentAsync());
        Guid betoId = GetUserId(await CreateAgentAsync());
        Guid ticketId = await CreateTicketAsync(sectorId);

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Gatilho temporário que segura por um segundo a troca de responsável, com a linha bloqueada: a outra
        // atribuição lê o mesmo chamado nesse intervalo e, ao gravar, encontra outra versão (conflito de concorrência).
        await context.Database.ExecuteSqlRawAsync(DelayTicketAssigneeChangeSql);

        HttpResponseMessage[] responses;

        try
        {
            // Act
            responses = await Task.WhenAll(SendAssignAsync(ticketId, anaId), SendAssignAsync(ticketId, betoId));
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync(DropDelayTicketAssigneeChangeSql);
        }

        // Assert
        responses.Select(r => r.StatusCode).Order().ShouldBe([HttpStatusCode.NoContent, HttpStatusCode.Conflict]);
        GetHistoryAfterCreation(ticketId).ShouldHaveSingleItem().Action.ShouldBe(TicketHistoryAction.Assigned);
    }

    private const string DelayTicketAssigneeChangeSql =
        """
        CREATE OR REPLACE FUNCTION public.test_delay_ticket_assignee_change() RETURNS trigger AS $$
        BEGIN
            PERFORM pg_sleep(1);
            RETURN NEW;
        END;
        $$ LANGUAGE plpgsql;

        CREATE TRIGGER test_delay_ticket_assignee_change
            BEFORE UPDATE ON public.tickets
            FOR EACH ROW
            WHEN (OLD.assigned_to_user_id IS DISTINCT FROM NEW.assigned_to_user_id)
            EXECUTE FUNCTION public.test_delay_ticket_assignee_change();
        """;

    private const string DropDelayTicketAssigneeChangeSql =
        """
        DROP TRIGGER IF EXISTS test_delay_ticket_assignee_change ON public.tickets;
        DROP FUNCTION IF EXISTS public.test_delay_ticket_assignee_change();
        """;

    /// <summary>
    /// Atribuição com o token já configurado no <see cref="BaseIntegrationTest.HttpClient"/>.
    /// </summary>
    private Task<HttpResponseMessage> SendAssignAsync(Guid ticketId, Guid assignedToUserId) =>
        HttpClient.PutAsJsonAsync($"tickets/{ticketId}/assignee", new { assignedToUserId });

    private Guid? GetAssignee(Guid ticketId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return context.Tickets.AsNoTracking().Where(t => t.Id == ticketId).Select(t => t.AssignedToUserId).Single();
    }

    /// <summary>
    /// Atendente novo com nome próprio e um sobrenome único (para o nome completo não se repetir entre os testes);
    /// deixa o administrador autenticado.
    /// </summary>
    private async Task<(Guid UserId, string FullName)> CreateNamedAgentAsync(string firstName, string lastName)
    {
        string uniqueLastName = $"{lastName}-{Guid.NewGuid():N}";
        HttpResponseMessage registerResponse = await PostRegisterAsAdminAsync(new
        {
            email = UniqueEmail(),
            firstName,
            lastName = uniqueLastName,
            password = "Password123!"
        });
        registerResponse.EnsureSuccessStatusCode();
        Guid userId = await registerResponse.Content.ReadFromJsonAsync<Guid>();

        await AuthenticateAsAdminAsync();
        (await HttpClient.PutAsJsonAsync($"users/{userId}/roles", new { roleNames = new[] { RoleNames.Agent } }))
            .EnsureSuccessStatusCode();

        return (userId, $"{firstName} {uniqueLastName}");
    }

    private string GetFullName(Guid userId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return context.Users.Where(u => u.Id == userId).Select(u => u.FirstName + " " + u.LastName).Single();
    }
}
