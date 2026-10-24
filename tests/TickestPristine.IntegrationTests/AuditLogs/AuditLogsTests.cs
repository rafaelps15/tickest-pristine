using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.AuditLogs;

public sealed class AuditLogsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private async Task<Guid> AuthenticateAsAdminAsync()
    {
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);

        return GetUserId(tokens);
    }

    private async Task<Guid> CreateDepartmentAsync(string name)
    {
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("departments", new
        {
            name,
            description = "A department created by tests"
        });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<List<AuditLogDto>> GetHistoryAsync(string entityName, Guid entityId)
    {
        HttpResponseMessage response = await HttpClient.GetAsync($"audit-logs/{entityName}/{entityId}");
        response.EnsureSuccessStatusCode();

        List<AuditLogDto>? history = (await response.Content.ReadFromJsonAsync<CursorPageDto<AuditLogDto>>())?.Items;

        return history!;
    }

    /// <summary>
    /// Lê uma propriedade do JSON gravado (o jsonb do PostgreSQL reformata o texto, então não dá para comparar strings).
    /// </summary>
    private static string? ReadValue(string? json, string property)
    {
        using var document = JsonDocument.Parse(json!);

        return document.RootElement.TryGetProperty(property, out JsonElement value) ? value.ToString() : null;
    }

    [Fact]
    public async Task GetHistory_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"audit-logs/Department/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetHistory_Should_ReturnForbidden_WhenCallerLacksAuditReadPermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"audit-logs/Department/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetHistory_Should_ContainAddedEntry_WhenEntityWasCreated()
    {
        // Arrange
        Guid adminId = await AuthenticateAsAdminAsync();
        string name = $"Department-{Guid.NewGuid():N}";
        Guid departmentId = await CreateDepartmentAsync(name);

        // Act
        List<AuditLogDto> history = await GetHistoryAsync("Department", departmentId);

        // Assert
        AuditLogDto added = history.ShouldHaveSingleItem();
        added.Action.ShouldBe("Added");
        added.UserId.ShouldBe(adminId);
        added.OldValues.ShouldBeNull();
        ReadValue(added.NewValues, "Name").ShouldBe(name);
    }

    [Fact]
    public async Task GetHistory_Should_ContainOnlyModifiedColumns_WhenEntityWasUpdated()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string originalName = $"Department-{Guid.NewGuid():N}";
        string newName = $"Department-{Guid.NewGuid():N}";
        Guid departmentId = await CreateDepartmentAsync(originalName);

        HttpResponseMessage updateResponse = await HttpClient.PutAsJsonAsync($"departments/{departmentId}", new
        {
            name = newName,
            description = "A department created by tests"
        });
        updateResponse.EnsureSuccessStatusCode();

        // Act
        List<AuditLogDto> history = await GetHistoryAsync("Department", departmentId);

        // Assert
        history.Count.ShouldBe(2);
        AuditLogDto modified = history[0];
        modified.Action.ShouldBe("Modified");
        modified.AffectedColumns.ShouldBe("Name");
        ReadValue(modified.OldValues, "Name").ShouldBe(originalName);
        ReadValue(modified.NewValues, "Name").ShouldBe(newName);
        ReadValue(modified.NewValues, "Description").ShouldBeNull();
    }

    [Fact]
    public async Task Register_Should_AuditCredentialWithoutPasswordHash()
    {
        // Arrange
        Guid userId = await RegisterUserAsync(UniqueEmail());

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid credentialId = await context.UserCredentials
            .Where(c => c.UserId == userId)
            .Select(c => c.Id)
            .SingleAsync();

        Guid adminId = await AuthenticateAsAdminAsync();

        // Act
        List<AuditLogDto> history = await GetHistoryAsync("UserCredential", credentialId);

        // Assert: o cadastro é feito pelo administrador, que fica registrado como autor da inclusão.
        AuditLogDto added = history.ShouldHaveSingleItem();
        added.UserId.ShouldBe(adminId);
        ReadValue(added.NewValues, "UserId").ShouldBe(userId.ToString());
        ReadValue(added.NewValues, "PasswordHash").ShouldBeNull();
    }

    private sealed record AuditLogDto(
        Guid Id,
        string EntityName,
        string EntityId,
        string Action,
        Guid? UserId,
        DateTime Timestamp,
        string? OldValues,
        string? NewValues,
        string? AffectedColumns);
}
