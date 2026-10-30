using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Departments;

public sealed class DepartmentsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
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
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

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

    private sealed record DepartmentDto(Guid Id, string Name, string Description, bool IsActive, Guid? ResponsibleUserId);

    private sealed record DepartmentSummaryDto(Guid Id, string Name, string Description);

    private sealed record SectorSummaryDto(Guid Id, string Name);

    private sealed record ProblemDto(int Status, string Title, string Detail);

    private sealed record ValidationProblemDto(int Status, string Title, List<ValidationErrorDto> Errors);

    private sealed record ValidationErrorDto(string Code, string Description);
}
