using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using TickestPristine.Application.Authorization;

namespace TickestPristine.IntegrationTests.Users;

public sealed class UsersTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Register_Should_ReturnUserId()
    {
        // Arrange
        string email = UniqueEmail();

        // Act
        Guid userId = await RegisterUserAsync(email);

        // Assert
        userId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Register_Should_ReturnBadRequest_WhenPasswordIsWeak()
    {
        // Arrange
        var request = new
        {
            email = UniqueEmail(),
            firstName = "Test",
            lastName = "User",
            password = "password123"
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/register", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_Should_ReturnAccessAndRefreshTokens()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        // Act
        AccessTokens tokens = await LoginAsync(email);

        // Assert
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_Should_IncludeIdentityClaims_WhenCredentialsAreValid()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        var handler = new JwtSecurityTokenHandler();

        // Act
        AccessTokens tokens = await LoginAsync(email);

        // Assert
        JwtSecurityToken jwt = handler.ReadJwtToken(tokens.AccessToken);
        jwt.Subject.ShouldBe(userId.ToString());
        jwt.Claims.Single(c => c.Type == "email").Value.ShouldBe(email);
        jwt.Claims.Single(c => c.Type == "name").Value.ShouldBe("Test User");
        jwt.Claims.Where(c => c.Type == "permissions").Select(c => c.Value).ShouldContain(PermissionCodes.Tickets.Create);
    }

    [Fact]
    public async Task Login_Should_IncludeEveryPermissionClaim_WhenUserIsSeededAdmin()
    {
        // Arrange
        var handler = new JwtSecurityTokenHandler();

        // Act
        AccessTokens tokens = await LoginAsync("admin@tickestpristine.dev", "ChangeMe123!");

        // Assert
        JwtSecurityToken jwt = handler.ReadJwtToken(tokens.AccessToken);
        var permissions = jwt.Claims.Where(c => c.Type == "permissions").Select(c => c.Value).ToList();
        permissions.ShouldNotBeEmpty();
        permissions.ShouldBe(PermissionCodes.All, ignoreOrder: true);
    }

    [Fact]
    public async Task Login_Should_ReturnSameGenericError_WhenEmailDoesNotExistOrPasswordIsWrong()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        var wrongPassword = new { email, password = "WrongPassword1!" };
        var unknownEmail = new { email = UniqueEmail(), password = "Password123!" };

        // Act
        HttpResponseMessage wrongPasswordResponse = await HttpClient.PostAsJsonAsync("users/login", wrongPassword);
        HttpResponseMessage unknownEmailResponse = await HttpClient.PostAsJsonAsync("users/login", unknownEmail);

        // Assert
        wrongPasswordResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        unknownEmailResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        ProblemDto? wrongPasswordProblem = await wrongPasswordResponse.Content.ReadFromJsonAsync<ProblemDto>();
        ProblemDto? unknownEmailProblem = await unknownEmailResponse.Content.ReadFromJsonAsync<ProblemDto>();

        wrongPasswordProblem!.Title.ShouldBe("Users.InvalidCredentials");
        wrongPasswordProblem.Detail.ShouldBe("E-mail ou senha inválidos");
        unknownEmailProblem.ShouldBe(wrongPasswordProblem);
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnNewTokens()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = tokens.RefreshToken });

        // Assert
        response.EnsureSuccessStatusCode();
        AccessTokens? rotated = await response.Content.ReadFromJsonAsync<AccessTokens>();
        rotated!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        rotated.RefreshToken.ShouldNotBe(tokens.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnProblem_WhenTokenIsInvalid()
    {
        // Arrange
        var request = new { refreshToken = "this-token-does-not-exist" };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/refresh-token", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAll_Should_ReturnUsers_WhenCallerIsAdmin()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync("admin@tickestpristine.dev", "ChangeMe123!");
        Authenticate(adminTokens.AccessToken);
        Guid userId = await RegisterUserAsync(UniqueEmail());

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users");

        // Assert
        response.EnsureSuccessStatusCode();
        List<UserSummaryDto>? users = await response.Content.ReadFromJsonAsync<List<UserSummaryDto>>();
        users!.ShouldContain(u => u.Id == userId);
    }

    [Fact]
    public async Task GetAll_Should_ReturnForbidden_WhenCallerLacksUsersManagePermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAll_Should_ReturnAssignedRoles_ForEachUser()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync("admin@tickestpristine.dev", "ChangeMe123!");
        Authenticate(adminTokens.AccessToken);

        HttpResponseMessage createRoleResponse = await HttpClient.PostAsJsonAsync("roles", new { name = $"Role-{Guid.NewGuid():N}" });
        createRoleResponse.EnsureSuccessStatusCode();
        Guid roleId = await createRoleResponse.Content.ReadFromJsonAsync<Guid>();

        Guid userId = await RegisterUserAsync(UniqueEmail());
        HttpResponseMessage assignResponse = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/roles",
            new { roleIds = new[] { roleId } });
        assignResponse.EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users");

        // Assert
        response.EnsureSuccessStatusCode();
        List<UserSummaryDto>? users = await response.Content.ReadFromJsonAsync<List<UserSummaryDto>>();
        UserSummaryDto user = users!.Single(u => u.Id == userId);
        user.Roles.ShouldContain(r => r.Id == roleId);
    }

    [Fact]
    public async Task GetByEmail_Should_ReturnForbidden_WhenCallerLacksUsersManagePermission()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        (_, AccessTokens callerTokens) = await RegisterAndLoginAsync();
        Authenticate(callerTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/by-email?email={Uri.EscapeDataString(email)}");

        // Assert: sem users:read, o usuário não consegue saber se um e-mail existe
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetByEmail_Should_ReturnUser_WhenCallerIsAdmin()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        AccessTokens adminTokens = await LoginAsync("admin@tickestpristine.dev", "ChangeMe123!");
        Authenticate(adminTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/by-email?email={Uri.EscapeDataString(email)}");

        // Assert
        response.EnsureSuccessStatusCode();
        UserByEmailDto? user = await response.Content.ReadFromJsonAsync<UserByEmailDto>();
        user!.Email.ShouldBe(email);
    }

    [Fact]
    public async Task UpdateProfile_Should_UpdateOwnProfile()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/profile",
            new { firstName = "Novo", lastName = "Nome" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        UserByEmailDto? user = await HttpClient.GetFromJsonAsync<UserByEmailDto>($"users/{userId}");
        user!.FirstName.ShouldBe("Novo");
        user.LastName.ShouldBe("Nome");
    }

    [Fact]
    public async Task UpdateProfile_Should_ReturnForbidden_WhenUpdatingAnotherUserWithoutManagePermission()
    {
        // Arrange
        Guid otherUserId = await RegisterUserAsync(UniqueEmail());
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{otherUserId}/profile",
            new { firstName = "Novo", lastName = "Nome" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateProfile_Should_ReturnBadRequest_WhenFirstNameIsEmpty()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/profile",
            new { firstName = string.Empty, lastName = "Nome" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_Should_AllowLoginWithNewPasswordOnly()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            "users/me/password",
            new { currentPassword = "Password123!", newPassword = "NewPassword456#" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage oldLogin = await HttpClient.PostAsJsonAsync(
            "users/login",
            new { email, password = "Password123!" });
        oldLogin.IsSuccessStatusCode.ShouldBeFalse();

        AccessTokens newTokens = await LoginAsync(email, "NewPassword456#");
        newTokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ChangePassword_Should_RevokeExistingRefreshTokens()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            "users/me/password",
            new { currentPassword = "Password123!", newPassword = "NewPassword456#" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage refresh = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = tokens.RefreshToken });
        refresh.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnBadRequest_WhenCurrentPasswordIsWrong()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            "users/me/password",
            new { currentPassword = "WrongPassword1!", newPassword = "NewPassword456#" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnBadRequest_WhenNewPasswordIsWeak()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            "users/me/password",
            new { currentPassword = "Password123!", newPassword = "weakpassword" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        var request = new { currentPassword = "Password123!", newPassword = "NewPassword456#" };

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync("users/me/password", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed record UserSummaryDto(Guid Id, string Email, string FirstName, string LastName, List<RoleSummaryDto> Roles);

    private sealed record RoleSummaryDto(Guid Id, string Name);

    private sealed record UserByEmailDto(Guid Id, string Email, string FirstName, string LastName);

    private sealed record ProblemDto(int Status, string Title, string Detail);
}
