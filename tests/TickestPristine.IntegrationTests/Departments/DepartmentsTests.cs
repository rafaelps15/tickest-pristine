using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Departments;

public sealed class DepartmentsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string TicketDescription = "The printer on the second floor is not working";

    private async Task AuthenticateAsAdminAsync()
    {
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);
    }

    private async Task<Guid> CreateDepartmentAsync(string? name = null)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("departments", new
        {
            name = name ?? $"Department-{Guid.NewGuid():N}",
            description = "A department created by tests"
        });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<Guid> CreateSectorAsync(Guid departmentId, string name)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("sectors", new { name, departmentId });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<Guid> CreateTicketAsync(Guid sectorId)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = "Printer is broken",
            description = TicketDescription,
            priority = 2,
            sectorId
        });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task ChangeTicketStatusAsync(Guid ticketId, params TicketStatus[] statuses)
    {
        foreach (TicketStatus status in statuses)
        {
            HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
            {
                description = TicketDescription,
                status = (int)status
            });
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
    }

    // TI com Suporte (2 Abertos e 1 Em andamento), Redes (1 Resolvido, 1 Cancelado e 1 Aberto excluído) e Infra, sem
    // chamados; Financeiro, sem setores. Tudo pela API.
    private async Task<CommonData> CreateCommonDataAsync()
    {
        Guid itId = await CreateDepartmentAsync();
        Guid financeId = await CreateDepartmentAsync();
        Guid supportId = await CreateSectorAsync(itId, "Suporte");
        Guid networksId = await CreateSectorAsync(itId, "Redes");
        Guid infrastructureId = await CreateSectorAsync(itId, "Infra");

        List<Guid> supportTicketIds =
        [
            await CreateTicketAsync(supportId),
            await CreateTicketAsync(supportId),
            await CreateTicketAsync(supportId)
        ];
        await ChangeTicketStatusAsync(supportTicketIds[2], TicketStatus.InProgress);

        await ChangeTicketStatusAsync(await CreateTicketAsync(networksId), TicketStatus.InProgress, TicketStatus.Resolved);
        await ChangeTicketStatusAsync(await CreateTicketAsync(networksId), TicketStatus.Canceled);
        (await HttpClient.DeleteAsync($"tickets/{await CreateTicketAsync(networksId)}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        return new CommonData(itId, financeId, supportId, networksId, infrastructureId, supportTicketIds);
    }

    private async Task<DepartmentListItemDto> GetListedDepartmentAsync(Guid departmentId)
    {
        List<DepartmentListItemDto>? departments = await HttpClient.GetFromJsonAsync<List<DepartmentListItemDto>>("departments");

        return departments!.Single(d => d.Id == departmentId);
    }

    [Fact]
    public async Task GetAll_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("departments");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_Should_ReturnDepartmentId_WhenCallerIsAdmin()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        string name = $"Department-{Guid.NewGuid():N}";

        // Act
        Guid departmentId = await CreateDepartmentAsync(name);

        // Assert
        departmentId.ShouldNotBe(Guid.Empty);

        DepartmentDto? department = await HttpClient.GetFromJsonAsync<DepartmentDto>($"departments/{departmentId}");
        department!.Name.ShouldBe(name);
        department.Description.ShouldBe("A department created by tests");
        department.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_Should_ReturnForbidden_WhenCallerLacksDepartmentsManagePermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("departments", new
        {
            name = $"Department-{Guid.NewGuid():N}",
            description = "A department created by tests"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetById_Should_ReturnNotFound_WhenDepartmentDoesNotExist()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"departments/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_Should_ReflectChangesInGetById()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();

        // Act
        HttpResponseMessage updateResponse = await HttpClient.PutAsJsonAsync($"departments/{departmentId}", new
        {
            name = "Customer Support",
            description = "Updated description"
        });

        // Assert
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"departments/{departmentId}");
        getResponse.EnsureSuccessStatusCode();
        DepartmentDto? department = await getResponse.Content.ReadFromJsonAsync<DepartmentDto>();
        department!.Name.ShouldBe("Customer Support");
        department.Description.ShouldBe("Updated description");
    }

    [Fact]
    public async Task Deactivate_Should_RemoveDepartmentFromGetAll()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string uniqueName = $"Department-{Guid.NewGuid():N}";
        Guid departmentId = await CreateDepartmentAsync(uniqueName);

        // Act
        HttpResponseMessage deleteResponse = await HttpClient.DeleteAsync($"departments/{departmentId}");

        // Assert
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage getAllResponse = await HttpClient.GetAsync("departments");
        getAllResponse.EnsureSuccessStatusCode();
        List<DepartmentSummaryDto>? departments = await getAllResponse.Content.ReadFromJsonAsync<List<DepartmentSummaryDto>>();
        departments!.ShouldNotContain(d => d.Id == departmentId);
    }

    [Fact]
    public async Task Create_Should_ReturnConflict_WhenActiveDepartmentHasSameNameIgnoringCase()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string name = $"Department-{Guid.NewGuid():N}";
        await CreateDepartmentAsync(name);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("departments", new
        {
            name = name.ToUpperInvariant(),
            description = "A department created by tests"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_Should_BeRejectedByDatabase_WhenNameIsLongerThanColumnLimit()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string name = Guid.NewGuid().ToString("N").PadRight(101, 'a');

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("departments", new
        {
            name,
            description = "A department created by tests"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Departments.AnyAsync(d => d.Name == name)).ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivate_Should_DeactivateItsSectors()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();
        HttpResponseMessage createSectorResponse = await HttpClient.PostAsJsonAsync("sectors", new
        {
            name = $"Sector-{Guid.NewGuid():N}",
            departmentId
        });
        createSectorResponse.EnsureSuccessStatusCode();
        Guid sectorId = await createSectorResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        HttpResponseMessage deleteResponse = await HttpClient.DeleteAsync($"departments/{departmentId}");

        // Assert
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        List<SectorSummaryDto>? sectors = await HttpClient.GetFromJsonAsync<List<SectorSummaryDto>>("sectors");
        sectors!.ShouldNotContain(s => s.Id == sectorId);
    }

    [Fact]
    public async Task Deactivate_Should_ReturnConflict_WhenSectorHasOpenTickets()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();
        HttpResponseMessage createSectorResponse = await HttpClient.PostAsJsonAsync("sectors", new
        {
            name = $"Sector-{Guid.NewGuid():N}",
            departmentId
        });
        createSectorResponse.EnsureSuccessStatusCode();
        Guid sectorId = await createSectorResponse.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage createTicketResponse = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = "Printer is broken",
            description = "The printer on the second floor is not working",
            priority = 2,
            sectorId
        });
        createTicketResponse.EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage deleteResponse = await HttpClient.DeleteAsync($"departments/{departmentId}");

        // Assert
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_Should_ReturnNotFound_WhenDepartmentDoesNotExist()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"departments/{Guid.NewGuid()}", new
        {
            name = "Customer Support",
            description = "Updated description"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deactivate_Should_ReturnConflict_WhenDepartmentIsAlreadyDeactivated()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();
        (await HttpClient.DeleteAsync($"departments/{departmentId}")).EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"departments/{departmentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_Should_ReturnBadRequest_WhenNameIsEmpty()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("departments", new
        {
            name = string.Empty,
            description = "A department created by tests"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // A mensagem usa o nome do campo em português, não o da propriedade.
        ValidationProblemDto? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDto>();
        problem!.Errors.ShouldContain(e => e.Description.Contains("Nome", StringComparison.Ordinal));
        problem.Errors.ShouldNotContain(e => e.Description.Contains("Name", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_Should_ReturnNotFound_WhenResponsibleUserDoesNotExist()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("departments", new
        {
            name = $"Department-{Guid.NewGuid():N}",
            description = "A department created by tests",
            responsibleUserId = Guid.NewGuid()
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.NotFound");
    }

    [Fact]
    public async Task Update_Should_ReturnConflict_WhenAnotherActiveDepartmentHasTheName()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string takenName = $"Department-{Guid.NewGuid():N}";
        await CreateDepartmentAsync(takenName);
        Guid departmentId = await CreateDepartmentAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"departments/{departmentId}", new
        {
            name = takenName,
            description = "Updated description"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Departments.NameNotUnique");
    }

    [Fact]
    public async Task Update_Should_ReturnNotFound_WhenResponsibleUserDoesNotExist()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"departments/{departmentId}", new
        {
            name = $"Department-{Guid.NewGuid():N}",
            description = "Updated description",
            responsibleUserId = Guid.NewGuid()
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.NotFound");
    }

    [Fact]
    public async Task Deactivate_Should_ReturnNotFound_WhenDepartmentDoesNotExist()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"departments/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_Should_ReturnActiveTicketCountOfDepartmentsAndSectors()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        CommonData data = await CreateCommonDataAsync();

        // Act
        List<DepartmentListItemDto>? departments = await HttpClient.GetFromJsonAsync<List<DepartmentListItemDto>>("departments");

        // Assert
        DepartmentListItemDto it = departments!.Single(d => d.Id == data.ItId);
        it.ActiveTicketCount.ShouldBe(3);
        it.Sectors.Single(s => s.Id == data.SupportId).ActiveTicketCount.ShouldBe(3);
        it.Sectors.Single(s => s.Id == data.NetworksId).ActiveTicketCount.ShouldBe(0);
        it.Sectors.Single(s => s.Id == data.InfrastructureId).ActiveTicketCount.ShouldBe(0);
        departments!.Single(d => d.Id == data.FinanceId).ActiveTicketCount.ShouldBe(0);
    }

    [Fact]
    public async Task Deactivate_Should_ReturnConflictWithSameCodeAndText_WhenListingShowsActiveTickets()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        CommonData data = await CreateCommonDataAsync();
        int listedCount = (await GetListedDepartmentAsync(data.ItId)).ActiveTicketCount;

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"departments/{data.ItId}");

        // Assert
        listedCount.ShouldBe(3);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Departments.HasActiveTickets");
        problem.Detail.ShouldBe(
            "O departamento tem chamados em aberto ou em andamento nos seus setores. Conclua ou cancele esses chamados antes de desativá-lo.");
    }

    [Fact]
    public async Task Deactivate_Should_ReturnNoContent_WhenActiveTicketsAreConcludedAndListingShowsZero()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        CommonData data = await CreateCommonDataAsync();
        await ChangeTicketStatusAsync(data.SupportTicketIds[0], TicketStatus.InProgress, TicketStatus.Resolved);
        await ChangeTicketStatusAsync(data.SupportTicketIds[1], TicketStatus.InProgress, TicketStatus.Resolved);
        await ChangeTicketStatusAsync(data.SupportTicketIds[2], TicketStatus.Resolved);
        int listedCount = (await GetListedDepartmentAsync(data.ItId)).ActiveTicketCount;

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"departments/{data.ItId}");

        // Assert
        listedCount.ShouldBe(0);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Deactivate_Should_ReturnConflictCountedByListing_WhenTicketIsReopenedInDeactivatedSector()
    {
        // Arrange: o chamado de Redes é resolvido, o setor é desativado e o chamado é reaberto, voltando a ser ativo num
        // setor desativado.
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();
        Guid networksId = await CreateSectorAsync(departmentId, "Redes");
        Guid ticketId = await CreateTicketAsync(networksId);
        await ChangeTicketStatusAsync(ticketId, TicketStatus.InProgress, TicketStatus.Resolved);
        (await HttpClient.DeleteAsync($"sectors/{networksId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await HttpClient.PostAsJsonAsync($"tickets/{ticketId}/reopen", new { })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        DepartmentListItemDto listed = await GetListedDepartmentAsync(departmentId);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"departments/{departmentId}");

        // Assert
        listed.ActiveTicketCount.ShouldBe(1);
        listed.Sectors.ShouldNotContain(s => s.Id == networksId);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Departments.HasActiveTickets");
    }

    private sealed record DepartmentDto(Guid Id, string Name, string Description, bool IsActive, Guid? ResponsibleUserId);

    private sealed record DepartmentSummaryDto(Guid Id, string Name, string Description);

    private sealed record SectorSummaryDto(Guid Id, string Name);

    private sealed record ProblemDto(int Status, string Title, string Detail);

    private sealed record ValidationProblemDto(int Status, string Title, List<ValidationErrorDto> Errors);

    private sealed record ValidationErrorDto(string Code, string Description);

    private sealed record DepartmentListItemDto(Guid Id, int ActiveTicketCount, List<DepartmentSectorListItemDto> Sectors);

    private sealed record DepartmentSectorListItemDto(Guid Id, int ActiveTicketCount);

    private sealed record CommonData(
        Guid ItId,
        Guid FinanceId,
        Guid SupportId,
        Guid NetworksId,
        Guid InfrastructureId,
        List<Guid> SupportTicketIds);
}
