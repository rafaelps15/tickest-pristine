using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Roles;
using TickestPristine.Infrastructure.Database;

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

        AccessTokens tokens = await LoginAsync(email);
        Authenticate(tokens.AccessToken);
        UserByEmailDto? user = await HttpClient.GetFromJsonAsync<UserByEmailDto>($"users/{userId}");
        user!.Id.ShouldBe(userId);
        user.Email.ShouldBe(email);
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
    public async Task Login_Should_RemoveExpiredRefreshTokens_WhenUserLogsInAgain()
    {
        // Arrange: a sessão do primeiro login já expirou
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await LoginAsync(email);

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.RefreshTokens
            .Where(r => r.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.ExpiresOnUtc, DateTime.UtcNow.AddDays(-1)));

        // Act
        await LoginAsync(email);

        // Assert
        List<DateTime> remainingExpirations = await context.RefreshTokens
            .Where(r => r.UserId == userId)
            .Select(r => r.ExpiresOnUtc)
            .ToListAsync();
        remainingExpirations.ShouldHaveSingleItem().ShouldBeGreaterThan(DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_Should_LockAccount_WhenPasswordIsWrongFiveTimesInARow()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);

        for (int attempt = 1; attempt < 5; attempt++)
        {
            (await HttpClient.PostAsJsonAsync("users/login", new { email, password = "WrongPassword1!" }))
                .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // Act: a quinta senha errada bloqueia, e nem a senha certa entra depois
        HttpResponseMessage fifthAttempt = await HttpClient.PostAsJsonAsync("users/login", new { email, password = "WrongPassword1!" });
        HttpResponseMessage correctPassword = await HttpClient.PostAsJsonAsync("users/login", new { email, password = "Password123!" });

        // Assert
        fifthAttempt.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        correctPassword.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ProblemDto? problem = await correctPassword.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.LockedOut");
    }

    [Fact]
    public async Task Refresh_Should_RevokeAllSessions_WhenRotatedTokenIsReused()
    {
        // Arrange: o token antigo foi trocado há mais de 30 segundos e volta a ser usado
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);

        HttpResponseMessage refreshResponse = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = tokens.RefreshToken });
        refreshResponse.EnsureSuccessStatusCode();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.RefreshTokens
            .Where(r => r.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.RotatedAtUtc, DateTime.UtcNow.AddMinutes(-1)));

        // Act
        HttpResponseMessage reuseResponse = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = tokens.RefreshToken });

        // Assert
        reuseResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await context.RefreshTokens.AnyAsync(r => r.UserId == userId)).ShouldBeFalse();
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
    }

    [Fact]
    public async Task Login_Should_NotIncludePermissionClaims()
    {
        // Arrange: as permissões são resolvidas no servidor e lidas em GET /users/me, nunca no token
        var handler = new JwtSecurityTokenHandler();

        // Act
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);

        // Assert
        JwtSecurityToken jwt = handler.ReadJwtToken(tokens.AccessToken);
        jwt.Claims.ShouldNotContain(c => c.Type == "permissions");
    }

    [Fact]
    public async Task Login_Should_ReturnForbidden_WhenPasswordIsProvisional()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await MarkPasswordAsProvisionalAsync(userId);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/login", new { email, password = "Password123!" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.PasswordChangeRequired");
    }

    [Fact]
    public async Task ChangeRequiredPassword_Should_ReturnTokensAndReleaseLogin_WhenPasswordIsProvisional()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await MarkPasswordAsProvisionalAsync(userId);
        var request = new { email, currentPassword = "Password123!", newPassword = "NewPassword123!" };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/login/new-password", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccessTokens? tokens = await response.Content.ReadFromJsonAsync<AccessTokens>();
        GetUserId(tokens!).ShouldBe(userId);

        AccessTokens loginTokens = await LoginAsync(email, "NewPassword123!");
        loginTokens.AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ChangeRequiredPassword_Should_ReturnInvalidCredentials_WhenCurrentPasswordIsWrong()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await MarkPasswordAsProvisionalAsync(userId);
        var request = new { email, currentPassword = "WrongPassword1!", newPassword = "NewPassword123!" };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/login/new-password", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.InvalidCredentials");
    }

    [Fact]
    public async Task ChangeRequiredPassword_Should_ReturnBadRequest_WhenNewPasswordIsWeak()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await MarkPasswordAsProvisionalAsync(userId);
        var request = new { email, currentPassword = "Password123!", newPassword = "weak" };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/login/new-password", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        HttpResponseMessage loginResponse = await HttpClient.PostAsJsonAsync("users/login", new { email, password = "Password123!" });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ChangeRequiredPassword_Should_ReturnConflict_WhenPasswordIsNotProvisional()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        var request = new { email, currentPassword = "Password123!", newPassword = "NewPassword123!" };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/login/new-password", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.PasswordChangeNotRequired");

        AccessTokens tokens = await LoginAsync(email, "Password123!");
        tokens.AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ChangeRequiredPassword_Should_ReturnForbidden_WhenUserIsDeactivated()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        await MarkPasswordAsProvisionalAsync(userId);

        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        (await HttpClient.PutAsync($"users/{userId}/deactivate", null)).EnsureSuccessStatusCode();

        var request = new { email, currentPassword = "Password123!", newPassword = "NewPassword123!" };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/login/new-password", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.Deactivated");
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
    public async Task RefreshToken_Should_AcceptTokenOnlyOnce_WhenUsedConcurrently()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        var request = new { refreshToken = tokens.RefreshToken };

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(
            HttpClient.PostAsJsonAsync("users/refresh-token", request),
            HttpClient.PostAsJsonAsync("users/refresh-token", request));

        // Assert: a segunda chamada perde a corrida (409) ou chega depois da troca (400), nunca as duas passam.
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Single(r => r.StatusCode != HttpStatusCode.OK).StatusCode
            .ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshToken_Should_ReturnBadRequest_WhenTokenIsInvalid()
    {
        // Arrange
        var request = new { refreshToken = "this-token-does-not-exist" };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/refresh-token", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAll_Should_FindUser_WhenSearchDiffersOnlyInCase()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        // Act
        PagedDto<UserSummaryDto>? users = await HttpClient.GetFromJsonAsync<PagedDto<UserSummaryDto>>(
            $"users?search={email.ToUpperInvariant()}");

        // Assert
        users!.Items.ShouldHaveSingleItem().Id.ShouldBe(userId);
    }

    [Fact]
    public async Task Register_Should_ReturnConflict_WhenEmailDiffersOnlyInCase()
    {
        // Arrange
        string email = UniqueEmail();
        await RegisterUserAsync(email);
        var request = new { email = email.ToUpperInvariant(), firstName = "Test", lastName = "User", password = "Password123!" };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/register", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetAll_Should_ReturnUsers_WhenCallerIsAdmin()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users?search={email}");

        // Assert
        response.EnsureSuccessStatusCode();
        PagedDto<UserSummaryDto>? users = await response.Content.ReadFromJsonAsync<PagedDto<UserSummaryDto>>();
        UserSummaryDto user = users!.Items.ShouldHaveSingleItem();
        user.Id.ShouldBe(userId);
        user.IsActive.ShouldBeTrue();
        user.CreatedAtUtc.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-5));
        user.DeactivatedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task GetAll_Should_ReturnForbidden_WhenCallerLacksUsersReadPermission()
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
    public async Task GetAll_Should_ReturnAssignedRolesForEachUser()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        string roleName = await CreateTestRoleAsync();

        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        HttpResponseMessage assignResponse = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/roles",
            new { roleNames = new[] { roleName } });
        assignResponse.EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users?search={email}");

        // Assert
        response.EnsureSuccessStatusCode();
        PagedDto<UserSummaryDto>? users = await response.Content.ReadFromJsonAsync<PagedDto<UserSummaryDto>>();
        UserSummaryDto user = users!.Items.Single(u => u.Id == userId);
        user.Roles.ShouldContain(r => r.Name == roleName);
    }

    [Fact]
    public async Task GetByEmail_Should_ReturnForbidden_WhenCallerLacksUsersReadPermission()
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
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/by-email?email={Uri.EscapeDataString(email)}");

        // Assert
        response.EnsureSuccessStatusCode();
        UserByEmailDto? user = await response.Content.ReadFromJsonAsync<UserByEmailDto>();
        user!.Email.ShouldBe(email);
    }

    [Fact]
    public async Task GetByEmail_Should_ReturnNotFound_WhenNoUserHasThatEmail()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/by-email?email={Uri.EscapeDataString(UniqueEmail())}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.NotFoundByEmail");
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
    public async Task UpdateProfile_Should_ReturnForbidden_WhenUpdatingAnotherUserWithoutUpdatePermission()
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
    public async Task UpdateProfile_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{Guid.NewGuid()}/profile",
            new { firstName = "Novo", lastName = "Nome" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.NotFound");
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

    [Fact]
    public async Task GetCurrent_Should_ReturnLoggedUserWithDefaultRoleAndPermissions()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/me");

        // Assert
        response.EnsureSuccessStatusCode();
        CurrentUserDto? me = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        me!.Id.ShouldBe(userId);
        me.Email.ShouldBe(email);
        me.Roles.ShouldHaveSingleItem().Name.ShouldBe("Colaborador");
        me.Permissions.ShouldContain(PermissionCodes.Tickets.Create);
        me.Permissions.ShouldNotContain(PermissionCodes.Tickets.Manage);
    }

    [Fact]
    public async Task GetCurrent_Should_ReturnEveryPermission_WhenUserIsSeededAdmin()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/me");

        // Assert
        response.EnsureSuccessStatusCode();
        CurrentUserDto? me = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        me!.Permissions.ShouldBe(PermissionCatalog.All.Select(p => p.Code), ignoreOrder: true);
    }

    [Fact]
    public async Task GetCurrent_Should_ReturnForbidden_WhenUserWasDeactivatedAfterLogin()
    {
        // Arrange
        (Guid userId, AccessTokens userTokens) = await RegisterAndLoginAsync();

        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        (await HttpClient.PutAsync($"users/{userId}/deactivate", null)).EnsureSuccessStatusCode();

        Authenticate(userTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.Deactivated");
    }

    [Fact]
    public async Task GetCurrent_Should_ReturnUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Should_RevokeRefreshToken()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/logout",
            new { refreshToken = tokens.RefreshToken });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage refresh = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = tokens.RefreshToken });
        refresh.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Logout_Should_KeepRefreshToken_WhenTokenBelongsToAnotherUser()
    {
        // Arrange
        (_, AccessTokens otherTokens) = await RegisterAndLoginAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/logout",
            new { refreshToken = otherTokens.RefreshToken });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage refresh = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = otherTokens.RefreshToken });
        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_Should_ReturnUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/logout",
            new { refreshToken = "any-token" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignRoles_Should_ReturnNoContent_WhenCallerIsAdminAndRolesExist()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        string roleName = await CreateTestRoleAsync();

        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/roles",
            new { roleNames = new[] { roleName } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        PagedDto<UserSummaryDto>? users = await HttpClient.GetFromJsonAsync<PagedDto<UserSummaryDto>>($"users?search={email}");
        UserSummaryDto user = users!.Items.Single(u => u.Id == userId);
        user.Roles.ShouldHaveSingleItem().Name.ShouldBe(roleName);
    }

    [Fact]
    public async Task AssignRoles_Should_ReturnBadRequestAndKeepRoles_WhenRoleNamesIsMissing()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        string roleName = await CreateTestRoleAsync();

        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        (await HttpClient.PutAsJsonAsync($"users/{userId}/roles", new { roleNames = new[] { roleName } }))
            .EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"users/{userId}/roles", new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        PagedDto<UserSummaryDto>? users = await HttpClient.GetFromJsonAsync<PagedDto<UserSummaryDto>>($"users?search={email}");
        UserSummaryDto user = users!.Items.Single(u => u.Id == userId);
        user.Roles.ShouldHaveSingleItem().Name.ShouldBe(roleName);
    }

    [Fact]
    public async Task AssignRoles_Should_ReturnNotFound_WhenRoleDoesNotExist()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        Guid userId = await RegisterUserAsync(UniqueEmail());

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/roles",
            new { roleNames = new[] { $"Role-{Guid.NewGuid():N}" } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Roles.NotFound");
    }

    [Fact]
    public async Task AssignRoles_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        string roleName = await CreateTestRoleAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{Guid.NewGuid()}/roles",
            new { roleNames = new[] { roleName } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.NotFound");
    }

    [Fact]
    public async Task AssignRoles_Should_ReturnForbidden_WhenRoleGrantsAdministrativePermissionCallerLacks()
    {
        // Arrange: role que só atribui roles, dada a um usuário comum
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        string delegateRoleName = await CreateRoleWithPermissionsAsync("users:assign-roles");
        string privilegedRoleName = await CreateRoleWithPermissionsAsync("roles:manage");
        string ticketRoleName = await CreateRoleWithPermissionsAsync("tickets:manage");

        string delegateEmail = UniqueEmail();
        Guid delegateUserId = await RegisterUserAsync(delegateEmail);
        (await HttpClient.PutAsJsonAsync($"users/{delegateUserId}/roles", new { roleNames = new[] { delegateRoleName } }))
            .EnsureSuccessStatusCode();

        Guid targetUserId = await RegisterUserAsync(UniqueEmail());

        AccessTokens delegateTokens = await LoginAsync(delegateEmail);
        Authenticate(delegateTokens.AccessToken);

        // Act
        HttpResponseMessage escalation = await HttpClient.PutAsJsonAsync(
            $"users/{targetUserId}/roles",
            new { roleNames = new[] { privilegedRoleName } });

        HttpResponseMessage ticketOnly = await HttpClient.PutAsJsonAsync(
            $"users/{targetUserId}/roles",
            new { roleNames = new[] { ticketRoleName } });

        // Assert
        escalation.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ticketOnly.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AssignRoles_Should_ReturnForbidden_WhenCallerLacksAssignRolesPermission()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/roles",
            new { roleNames = Array.Empty<string>() });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignRoles_Should_ReturnConflict_WhenRemovingAdministratorRoleFromLastAdministrator()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        Guid adminUserId = GetUserId(adminTokens);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{adminUserId}/roles",
            new { roleNames = Array.Empty<string>() });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        HttpResponseMessage stillAdmin = await HttpClient.GetAsync("roles");
        stillAdmin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deactivate_Should_BlockLoginAndRefresh_WhenCallerHasDeactivatePermission()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens userTokens = await LoginAsync(email);

        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{userId}/deactivate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage loginResponse = await HttpClient.PostAsJsonAsync("users/login", new { email, password = "Password123!" });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ProblemDto? loginProblem = await loginResponse.Content.ReadFromJsonAsync<ProblemDto>();
        loginProblem!.Title.ShouldBe("Users.Deactivated");

        HttpResponseMessage refreshResponse = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = userTokens.RefreshToken });
        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deactivate_Should_KeepPreviousRefreshTokensInvalid_WhenUserIsReactivated()
    {
        // Arrange
        (Guid userId, AccessTokens userTokens) = await RegisterAndLoginAsync();

        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        (await HttpClient.PutAsync($"users/{userId}/deactivate", null)).EnsureSuccessStatusCode();
        (await HttpClient.PutAsync($"users/{userId}/activate", null)).EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = userTokens.RefreshToken });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.InvalidRefreshToken");
    }

    [Fact]
    public async Task Deactivate_Should_RevokePermissionsOfIssuedAccessToken()
    {
        // Arrange: com o token ainda válido, o usuário passa pela permissão e só falha na validação (400)
        (Guid userId, AccessTokens userTokens) = await RegisterAndLoginAsync();
        Authenticate(userTokens.AccessToken);
        (await HttpClient.PostAsJsonAsync("tickets", new { })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        (await HttpClient.PutAsync($"users/{userId}/deactivate", null)).EnsureSuccessStatusCode();

        Authenticate(userTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("tickets", new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deactivate_Should_BlockRoutesWithoutPermissionForIssuedAccessToken()
    {
        // Arrange: a rota de departamentos só exige autenticação, sem permissão própria
        (Guid userId, AccessTokens userTokens) = await RegisterAndLoginAsync();
        Authenticate(userTokens.AccessToken);
        (await HttpClient.GetAsync("departments")).StatusCode.ShouldBe(HttpStatusCode.OK);

        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        (await HttpClient.PutAsync($"users/{userId}/deactivate", null)).EnsureSuccessStatusCode();

        Authenticate(userTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("departments");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deactivate_Should_ReturnBadRequest_WhenCallerDeactivatesOwnAccount()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        Guid adminUserId = GetUserId(adminTokens);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{adminUserId}/deactivate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.CannotDeactivateSelf");
    }

    [Fact]
    public async Task Deactivate_Should_ReturnConflict_WhenUserIsAlreadyDeactivated()
    {
        // Arrange
        Guid userId = await RegisterUserAsync(UniqueEmail());
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        (await HttpClient.PutAsync($"users/{userId}/deactivate", null)).EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{userId}/deactivate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Deactivate_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{Guid.NewGuid()}/deactivate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deactivate_Should_ReturnForbidden_WhenTargetHasAdministrativePermissionsCallerLacks()
    {
        // Arrange: quem chama só pode desativar colaboradores; o alvo também gerencia funções. Roles de teste, e não a
        // de Administrador, para não deixar outro administrador no banco compartilhado entre os testes.
        Authenticate((await LoginAsync(AdminEmail, AdminPassword)).AccessToken);
        string deactivatorRole = await CreateRoleWithPermissionsAsync(PermissionCodes.Users.Deactivate);
        string rolesManagerRole = await CreateRoleWithPermissionsAsync(PermissionCodes.Roles.Manage);

        string callerEmail = UniqueEmail();
        Guid callerId = await RegisterUserAsync(callerEmail);
        (await HttpClient.PutAsJsonAsync($"users/{callerId}/roles", new { roleNames = new[] { deactivatorRole } }))
            .EnsureSuccessStatusCode();

        Guid targetId = await RegisterUserAsync(UniqueEmail());
        (await HttpClient.PutAsJsonAsync($"users/{targetId}/roles", new { roleNames = new[] { rolesManagerRole } }))
            .EnsureSuccessStatusCode();

        Authenticate((await LoginAsync(callerEmail)).AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{targetId}/deactivate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.CannotManagePrivilegedUser");
    }

    [Fact]
    public async Task Deactivate_Should_ReturnConflict_WhenUserIsLastActiveAdministrator()
    {
        // Arrange
        // Quem chama tem todas as permissões por uma role de teste, sem ser administrador: o administrador do seed não
        // pode desativar a si mesmo, e os testes rodam em sequência sem deixar outro administrador no banco.
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        Guid adminUserId = GetUserId(adminTokens);

        string everyPermissionRole = await CreateRoleWithPermissionsAsync([.. PermissionCatalog.All.Select(p => p.Code)]);

        string callerEmail = UniqueEmail();
        Guid callerId = await RegisterUserAsync(callerEmail);
        (await HttpClient.PutAsJsonAsync($"users/{callerId}/roles", new { roleNames = new[] { everyPermissionRole } }))
            .EnsureSuccessStatusCode();

        Authenticate((await LoginAsync(callerEmail)).AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{adminUserId}/deactivate", null);

        try
        {
            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
            problem!.Title.ShouldBe("Roles.LastAdministrator");

            (await LoginAsync(AdminEmail, AdminPassword)).AccessToken.ShouldNotBeNullOrWhiteSpace();
        }
        finally
        {
            // Se a desativação passar por engano, reativa o administrador do seed, de que os outros testes dependem.
            if (response.IsSuccessStatusCode)
            {
                await HttpClient.PutAsync($"users/{adminUserId}/activate", null);
            }
        }
    }

    [Fact]
    public async Task Deactivate_Should_ReturnForbidden_WhenCallerLacksDeactivatePermission()
    {
        // Arrange
        Guid targetUserId = await RegisterUserAsync(UniqueEmail());
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{targetUserId}/deactivate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deactivate_Should_ReturnUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        Guid userId = await RegisterUserAsync(UniqueEmail());

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{userId}/deactivate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_Should_ReturnUserAsInactiveWithDeactivationDate_WhenUserWasDeactivated()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        (await HttpClient.PutAsync($"users/{userId}/deactivate", null)).EnsureSuccessStatusCode();

        // Act
        PagedDto<UserSummaryDto>? users = await HttpClient.GetFromJsonAsync<PagedDto<UserSummaryDto>>(
            $"users?status=Inactive&search={email}");

        // Assert
        UserSummaryDto user = users!.Items.ShouldHaveSingleItem();
        user.Id.ShouldBe(userId);
        user.IsActive.ShouldBeFalse();
        user.DeactivatedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAll_Should_NotReturnDeactivatedUser_WhenStatusIsActive()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        (await HttpClient.PutAsync($"users/{userId}/deactivate", null)).EnsureSuccessStatusCode();

        // Act
        PagedDto<UserSummaryDto>? users = await HttpClient.GetFromJsonAsync<PagedDto<UserSummaryDto>>(
            $"users?status=Active&search={email}");

        // Assert
        users!.Items.ShouldBeEmpty();
        users.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetAll_Should_AdjustPageAndPageSize_WhenOutOfRange()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        // Act
        PagedDto<UserSummaryDto>? users = await HttpClient.GetFromJsonAsync<PagedDto<UserSummaryDto>>(
            "users?page=0&pageSize=1000");

        // Assert
        users!.Page.ShouldBe(1);
        users.PageSize.ShouldBe(100);
    }

    [Fact]
    public async Task GetAll_Should_ReturnRequestedPage_WhenPageSizeIsGiven()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        await RegisterUserAsync(UniqueEmail());
        await RegisterUserAsync(UniqueEmail());

        // Act
        PagedDto<UserSummaryDto>? users = await HttpClient.GetFromJsonAsync<PagedDto<UserSummaryDto>>(
            "users?page=1&pageSize=1");

        // Assert
        users!.Items.Count.ShouldBe(1);
        users.Page.ShouldBe(1);
        users.PageSize.ShouldBe(1);
        users.TotalCount.ShouldBeGreaterThan(1);
        users.TotalPages.ShouldBe(users.TotalCount);
        users.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAll_Should_ReturnEmptyPage_WhenCreatedFromIsAfterCreatedTo()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        // Act
        PagedDto<UserSummaryDto>? users = await HttpClient.GetFromJsonAsync<PagedDto<UserSummaryDto>>(
            "users?createdFrom=2026-10-02&createdTo=2026-10-01");

        // Assert
        users!.Items.ShouldBeEmpty();
        users.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Activate_Should_RestoreLoginAndPermissions_WhenUserWasDeactivated()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens userTokens = await LoginAsync(email);

        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        (await HttpClient.PutAsync($"users/{userId}/deactivate", null)).EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{userId}/activate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        HttpResponseMessage loginResponse = await HttpClient.PostAsJsonAsync("users/login", new { email, password = "Password123!" });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Com as permissões de volta, o token antigo passa pela permissão e só falha na validação (400)
        Authenticate(userTokens.AccessToken);
        (await HttpClient.PostAsJsonAsync("tickets", new { })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Activate_Should_ReturnConflict_WhenUserIsAlreadyActive()
    {
        // Arrange
        Guid userId = await RegisterUserAsync(UniqueEmail());
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{userId}/activate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Activate_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{Guid.NewGuid()}/activate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Activate_Should_ReturnForbidden_WhenCallerLacksDeactivatePermission()
    {
        // Arrange
        Guid targetUserId = await RegisterUserAsync(UniqueEmail());
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{targetUserId}/activate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Activate_Should_ReturnForbidden_WhenTargetHasAdministrativePermissionsCallerLacks()
    {
        // Arrange
        // Quem chama só pode reativar colaboradores; o alvo, desativado pelo administrador, também gerencia funções.
        Authenticate((await LoginAsync(AdminEmail, AdminPassword)).AccessToken);
        string activatorRole = await CreateRoleWithPermissionsAsync(PermissionCodes.Users.Deactivate);
        string rolesManagerRole = await CreateRoleWithPermissionsAsync(PermissionCodes.Roles.Manage);

        string callerEmail = UniqueEmail();
        Guid callerId = await RegisterUserAsync(callerEmail);
        (await HttpClient.PutAsJsonAsync($"users/{callerId}/roles", new { roleNames = new[] { activatorRole } }))
            .EnsureSuccessStatusCode();

        Guid targetId = await RegisterUserAsync(UniqueEmail());
        (await HttpClient.PutAsJsonAsync($"users/{targetId}/roles", new { roleNames = new[] { rolesManagerRole } }))
            .EnsureSuccessStatusCode();
        (await HttpClient.PutAsync($"users/{targetId}/deactivate", null)).EnsureSuccessStatusCode();

        Authenticate((await LoginAsync(callerEmail)).AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{targetId}/activate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.CannotManagePrivilegedUser");
    }

    [Fact]
    public async Task Activate_Should_ReturnUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        Guid userId = await RegisterUserAsync(UniqueEmail());

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{userId}/activate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_Should_ReturnForbidden_WhenQueryingAnotherUserWithoutReadPermission()
    {
        // Arrange
        (Guid otherUserId, _) = await RegisterAndLoginAsync();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/{otherUserId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetById_Should_ReturnUser_WhenCallerIsSelf()
    {
        // Arrange
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/{userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        UserByEmailDto? user = await response.Content.ReadFromJsonAsync<UserByEmailDto>();
        user!.Id.ShouldBe(userId);
        user.Email.ShouldBe(email);
    }

    [Fact]
    public async Task GetById_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.NotFound");
    }

    private async Task<string> CreateRoleWithPermissionsAsync(params string[] permissionCodes)
    {
        string roleName = await CreateTestRoleAsync();

        (await HttpClient.PutAsJsonAsync($"roles/{roleName}/permissions", new { permissionCodes }))
            .EnsureSuccessStatusCode();

        return roleName;
    }

    private sealed record PagedDto<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages, bool HasPreviousPage, bool HasNextPage);

    private sealed record UserSummaryDto(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        bool IsActive,
        DateTime CreatedAtUtc,
        DateTime? DeactivatedAtUtc,
        List<RoleSummaryDto> Roles);

    private sealed record RoleSummaryDto(string Name);

    private sealed record UserByEmailDto(Guid Id, string Email, string FirstName, string LastName);

    private sealed record ProblemDto(int Status, string Title, string Detail);

    /// <summary>
    /// Deixa a senha do usuário provisória, como a do administrador criado pelo seed.
    /// </summary>
    private async Task MarkPasswordAsProvisionalAsync(Guid userId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.UserCredentials
            .Where(c => c.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.MustChangePassword, true));
    }

    private sealed record CurrentUserDto(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        List<RoleSummaryDto> Roles,
        List<string> Permissions);
}
