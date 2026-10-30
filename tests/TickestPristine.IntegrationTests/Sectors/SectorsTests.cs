using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Sectors;

public sealed class SectorsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private async Task AuthenticateAsAdminAsync()
    {
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);
    }

    private async Task<Guid> CreateDepartmentAsync()
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("departments", new
        {
            name = $"Department-{Guid.NewGuid():N}",
            description = "A department created by tests"
        });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<Guid> CreateSectorAsync(Guid departmentId, string? name = null)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("sectors", new
        {
            name = name ?? $"Sector-{Guid.NewGuid():N}",
            description = "A sector created by tests",
            departmentId
        });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    [Fact]
    public async Task GetAll_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("sectors");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_Should_ReturnSectorId_WhenDepartmentExists()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();
        string name = $"Sector-{Guid.NewGuid():N}";

        // Act
        Guid sectorId = await CreateSectorAsync(departmentId, name);

        // Assert
        sectorId.ShouldNotBe(Guid.Empty);

        SectorDetailDto? sector = await HttpClient.GetFromJsonAsync<SectorDetailDto>($"sectors/{sectorId}");
        sector!.Name.ShouldBe(name);
        sector.Description.ShouldBe("A sector created by tests");
        sector.DepartmentId.ShouldBe(departmentId);
    }

    [Fact]
    public async Task Create_Should_ReturnNotFound_WhenDepartmentDoesNotExist()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("sectors", new
        {
            name = $"Sector-{Guid.NewGuid():N}",
            description = "A sector created by tests",
            departmentId = Guid.NewGuid()
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Should_BeRejectedByDatabase_WhenNameIsLongerThanColumnLimit()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();
        string name = Guid.NewGuid().ToString("N").PadRight(101, 'a');

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("sectors", new
        {
            name,
            departmentId
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Sectors.AnyAsync(s => s.Name == name)).ShouldBeFalse();
    }

    [Fact]
    public async Task Create_Should_ReturnForbidden_WhenCallerLacksSectorsManagePermission()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();

        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("sectors", new
        {
            name = $"Sector-{Guid.NewGuid():N}",
            description = "A sector created by tests",
            departmentId
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetById_Should_ReturnNotFound_WhenSectorDoesNotExist()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"sectors/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_Should_ReflectChangesInGetById()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();
        Guid sectorId = await CreateSectorAsync(departmentId);

        // Act
        HttpResponseMessage updateResponse = await HttpClient.PutAsJsonAsync($"sectors/{sectorId}", new
        {
            name = "Technical Support",
            description = "Updated description"
        });

        // Assert
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage getResponse = await HttpClient.GetAsync($"sectors/{sectorId}");
        getResponse.EnsureSuccessStatusCode();
        SectorDto? sector = await getResponse.Content.ReadFromJsonAsync<SectorDto>();
        sector!.Name.ShouldBe("Technical Support");
        sector.Description.ShouldBe("Updated description");
    }

    [Fact]
    public async Task Deactivate_Should_RemoveSectorFromGetAll()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();
        Guid sectorId = await CreateSectorAsync(departmentId);

        // Act
        HttpResponseMessage deleteResponse = await HttpClient.DeleteAsync($"sectors/{sectorId}");

        // Assert
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage getAllResponse = await HttpClient.GetAsync("sectors");
        getAllResponse.EnsureSuccessStatusCode();
        List<SectorDto>? sectors = await getAllResponse.Content.ReadFromJsonAsync<List<SectorDto>>();
        sectors!.ShouldNotContain(s => s.Id == sectorId);
    }

    [Fact]
    public async Task Create_Should_ReturnConflict_WhenDepartmentHasSectorWithSameNameIgnoringCase()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();
        string name = $"Sector-{Guid.NewGuid():N}";
        await CreateSectorAsync(departmentId, name);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("sectors", new
        {
            name = name.ToUpperInvariant(),
            departmentId
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_Should_ReturnSectorId_WhenSameNameExistsInAnotherDepartment()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string name = $"Sector-{Guid.NewGuid():N}";
        await CreateSectorAsync(await CreateDepartmentAsync(), name);
        Guid otherDepartmentId = await CreateDepartmentAsync();

        // Act
        Guid sectorId = await CreateSectorAsync(otherDepartmentId, name);

        // Assert
        sectorId.ShouldNotBe(Guid.Empty);

        SectorDetailDto? sector = await HttpClient.GetFromJsonAsync<SectorDetailDto>($"sectors/{sectorId}");
        sector!.Name.ShouldBe(name);
        sector.DepartmentId.ShouldBe(otherDepartmentId);
    }

    [Fact]
    public async Task Update_Should_ReturnNotFound_WhenSectorDoesNotExist()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"sectors/{Guid.NewGuid()}", new
        {
            name = "Technical Support",
            description = "Updated description"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deactivate_Should_ReturnNotFound_WhenSectorDoesNotExist()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"sectors/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deactivate_Should_ReturnConflict_WhenSectorIsAlreadyDeactivated()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();
        Guid sectorId = await CreateSectorAsync(departmentId);
        (await HttpClient.DeleteAsync($"sectors/{sectorId}")).EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"sectors/{sectorId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_Should_ReturnBadRequest_WhenNameIsEmpty()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("sectors", new
        {
            name = string.Empty,
            description = "A sector created by tests",
            departmentId
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Should_ReturnNotFound_WhenDepartmentIsDeactivated()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();
        (await HttpClient.DeleteAsync($"departments/{departmentId}")).EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("sectors", new
        {
            name = $"Sector-{Guid.NewGuid():N}",
            description = "A sector created by tests",
            departmentId
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Departments.NotFound");
    }

    [Fact]
    public async Task Update_Should_ReturnConflict_WhenAnotherActiveSectorOfTheDepartmentHasTheName()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();
        string takenName = $"Sector-{Guid.NewGuid():N}";
        await CreateSectorAsync(departmentId, takenName);
        Guid sectorId = await CreateSectorAsync(departmentId);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"sectors/{sectorId}", new
        {
            name = takenName,
            description = "Updated description"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Sectors.NameNotUnique");
    }

    [Fact]
    public async Task Deactivate_Should_ReturnConflict_WhenSectorHasOpenTickets()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();
        Guid sectorId = await CreateSectorAsync(departmentId);

        HttpResponseMessage createTicketResponse = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = "Printer is broken",
            description = "The printer on the second floor is not working",
            priority = 2,
            sectorId
        });
        createTicketResponse.EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync($"sectors/{sectorId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Sectors.HasActiveTickets");
    }

    private sealed record SectorDto(Guid Id, string Name, string? Description);

    private sealed record SectorDetailDto(Guid Id, string Name, string? Description, Guid DepartmentId);

    private sealed record ProblemDto(int Status, string Title, string Detail);
}
