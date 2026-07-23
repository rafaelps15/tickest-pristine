using System.Net;
using System.Net.Http.Json;

namespace TickestPristine.IntegrationTests.Sectors;

public sealed class SectorsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string AdminEmail = "admin@tickestpristine.dev";
    private const string AdminPassword = "ChangeMe123!";

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
    public async Task Create_Should_ReturnSectorId_WhenDepartmentExists()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid departmentId = await CreateDepartmentAsync();

        // Act
        Guid sectorId = await CreateSectorAsync(departmentId);

        // Assert
        sectorId.ShouldNotBe(Guid.Empty);
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
    public async Task Update_Then_GetById_Should_ReflectChanges()
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
    public async Task Delete_Then_GetAll_Should_NotIncludeSector()
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

    private sealed record SectorDto(Guid Id, string Name, string? Description);
}
