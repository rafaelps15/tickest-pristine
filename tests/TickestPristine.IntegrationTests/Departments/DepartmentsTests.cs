using System.Net;
using System.Net.Http.Json;

namespace TickestPristine.IntegrationTests.Departments;

public sealed class DepartmentsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string AdminEmail = "admin@tickestpristine.dev";
    private const string AdminPassword = "ChangeMe123!";

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

    [Fact]
    public async Task Create_Should_ReturnDepartmentId_WhenCallerIsAdmin()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        Guid departmentId = await CreateDepartmentAsync();

        // Assert
        departmentId.ShouldNotBe(Guid.Empty);
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
    public async Task Update_Then_GetById_Should_ReflectChanges()
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
    public async Task Delete_Then_GetAll_Should_NotIncludeDepartment()
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

    private sealed record DepartmentDto(Guid Id, string Name, string Description, bool IsActive, Guid? ResponsibleUserId);

    private sealed record DepartmentSummaryDto(Guid Id, string Name, string Description);
}
