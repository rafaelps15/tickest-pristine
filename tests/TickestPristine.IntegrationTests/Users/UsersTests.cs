using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Application.Permissions;
using TickestPristine.Application.Roles;
using TickestPristine.Application.Users;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Users;
using TickestPristine.IntegrationTests.Tickets;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Users;

public sealed class UsersTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private readonly IntegrationTestWebAppFactory _factory = factory;

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
    public async Task Register_Should_ReturnBadRequest_WhenEmailIsInvalid()
    {
        // Arrange: o cadastro não recebe senha (spec 008); a política de senha vale na ativação
        var request = new
        {
            email = "sem-arroba",
            firstName = "Test",
            lastName = "User"
        };

        // Act
        HttpResponseMessage response = await PostRegisterAsAdminAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_Should_ReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;
        var request = new { email = UniqueEmail(), firstName = "Test", lastName = "User", password = "Password123!" };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/register", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_Should_ReturnForbidden_WhenCallerLacksUsersCreatePermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);
        var request = new { email = UniqueEmail(), firstName = "Test", lastName = "User", password = "Password123!" };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/register", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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
        HttpResponseMessage response = await PostRegisterAsAdminAsync(request);

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
    public async Task GetStatusCounts_Should_CountActivePendingAndInactiveSeparately()
    {
        // Arrange: o banco é compartilhado pelos testes (que rodam em série), então o teste confere a variação a partir
        // da contagem anterior
        await AuthenticateAsAdminAsync();
        UserStatusCountsDto before = await GetUserStatusCountsAsync();

        await RegisterUserAsync(UniqueEmail());                     // Ana: ativa, com senha
        await RegisterPendingUserAsync(UniqueEmail());              // Bruno: pendente
        Guid carla = await RegisterUserAsync(UniqueEmail());        // Carla: desativada depois de ativar a conta
        Guid davi = await RegisterPendingUserAsync(UniqueEmail());  // Davi: desativado antes de ativar
        (await HttpClient.PutAsync($"users/{carla}/deactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await HttpClient.PutAsync($"users/{davi}/deactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/stats/status");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        UserStatusCountsDto after = (await response.Content.ReadFromJsonAsync<UserStatusCountsDto>())!;
        (after.TotalCount - before.TotalCount).ShouldBe(4);
        (after.ActiveCount - before.ActiveCount).ShouldBe(1);
        (after.PendingActivationCount - before.PendingActivationCount).ShouldBe(1);
        (after.InactiveCount - before.InactiveCount).ShouldBe(2);
        (after.ActiveCount + after.PendingActivationCount + after.InactiveCount).ShouldBe(after.TotalCount);
    }

    [Fact]
    public async Task GetStatusCounts_Should_MovePendingUserToActive_WhenAccountIsActivated()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string email = UniqueEmail();
        await RegisterPendingUserAsync(email);
        UserStatusCountsDto before = await GetUserStatusCountsAsync();

        // Act
        (await ActivateAccountAsync(EmailSender.LatestTokenFor(email), "Password123!")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);
        UserStatusCountsDto after = await GetUserStatusCountsAsync();

        // Assert
        (after.PendingActivationCount - before.PendingActivationCount).ShouldBe(-1);
        (after.ActiveCount - before.ActiveCount).ShouldBe(1);
        after.InactiveCount.ShouldBe(before.InactiveCount);
        after.TotalCount.ShouldBe(before.TotalCount);
    }

    [Fact]
    public async Task GetStatusCounts_Should_MoveActiveUserToInactive_WhenUserIsDeactivated()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        Guid ana = await RegisterUserAsync(UniqueEmail());
        UserStatusCountsDto before = await GetUserStatusCountsAsync();

        // Act
        (await HttpClient.PutAsync($"users/{ana}/deactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        UserStatusCountsDto after = await GetUserStatusCountsAsync();

        // Assert
        (after.ActiveCount - before.ActiveCount).ShouldBe(-1);
        (after.InactiveCount - before.InactiveCount).ShouldBe(1);
        after.PendingActivationCount.ShouldBe(before.PendingActivationCount);
        after.TotalCount.ShouldBe(before.TotalCount);
    }

    [Fact]
    public async Task GetStatusCounts_Should_ReturnForbidden_WhenCallerLacksUsersReadPermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/stats/status");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetStatusCounts_Should_ReturnUnauthorized_WhenNotAuthenticated()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/stats/status");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStatusCounts_Should_CountInSingleSqlCommand()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        await RegisterPendingUserAsync(UniqueEmail());

        // Act: conta os comandos que leem credenciais (a contagem é o único que precisa delas)
        HttpResponseMessage response;
        int credentialQueries;
        int groupedCountQueries;

        using (var credentialCounter = new EfCommandCounter(sql => sql.Contains("user_credentials", StringComparison.Ordinal)))
        using (var groupedCounter = new EfCommandCounter(sql =>
            sql.Contains("user_credentials", StringComparison.Ordinal) &&
            sql.Contains("GROUP BY", StringComparison.Ordinal)))
        {
            response = await HttpClient.GetAsync("users/stats/status");
            credentialQueries = credentialCounter.Count;
            groupedCountQueries = groupedCounter.Count;
        }

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        credentialQueries.ShouldBe(1);
        groupedCountQueries.ShouldBe(1);
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

    /// <summary>
    /// Cadastra pelo administrador sem ativar a conta: o usuário fica pendente, com o convite no e-mail capturado.
    /// </summary>
    private async Task<Guid> RegisterPendingUserAsync(string email)
    {
        HttpResponseMessage response = await PostRegisterAsAdminAsync(new { email, firstName = "Test", lastName = "User" });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<UserStatusCountsDto> GetUserStatusCountsAsync() =>
        (await HttpClient.GetFromJsonAsync<UserStatusCountsDto>("users/stats/status"))!;

    private sealed record UserStatusCountsDto(int TotalCount, int ActiveCount, int InactiveCount, int PendingActivationCount);

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

    [Fact]
    public async Task ChangeSector_Should_SetRejectDeactivatedAndRemoveSector_WhenCallerIsAdmin()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string token = NewToken();
        Guid itSectorId = await InsertSectorAsync("TI", $"Tecnologia-{token}");
        Guid deactivatedSectorId = await InsertSectorAsync("X", $"Outro-{token}");
        (await HttpClient.DeleteAsync($"sectors/{deactivatedSectorId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Guid anaId = await InsertUserAsync("Ana", "Souza", $"ana.{token}@example.com");

        // Act
        HttpResponseMessage setResponse = await HttpClient.PutAsJsonAsync($"users/{anaId}/sector", new { sectorId = itSectorId });
        Guid? afterSet = await GetSectorIdAsync(anaId);
        HttpResponseMessage deactivatedResponse = await HttpClient.PutAsJsonAsync(
            $"users/{anaId}/sector",
            new { sectorId = deactivatedSectorId });
        Guid? afterDeactivated = await GetSectorIdAsync(anaId);
        HttpResponseMessage removeResponse = await HttpClient.PutAsJsonAsync(
            $"users/{anaId}/sector",
            new { sectorId = (Guid?)null });
        Guid? afterRemove = await GetSectorIdAsync(anaId);

        // Assert
        setResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        afterSet.ShouldBe(itSectorId);

        deactivatedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await deactivatedResponse.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Sectors.NotFound");
        afterDeactivated.ShouldBe(itSectorId);

        removeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        afterRemove.ShouldBeNull();
    }

    [Fact]
    public async Task ChangeSector_Should_KeepUserSector_WhenSectorIsDeactivated()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string token = NewToken();
        Guid itSectorId = await InsertSectorAsync("TI", $"Tecnologia-{token}");
        Guid anaId = await InsertUserAsync("Ana", "Souza", $"ana.{token}@example.com");
        (await HttpClient.PutAsJsonAsync($"users/{anaId}/sector", new { sectorId = itSectorId })).EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage deactivateResponse = await HttpClient.DeleteAsync($"sectors/{itSectorId}");

        // Assert
        deactivateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetSectorIdAsync(anaId)).ShouldBe(itSectorId);
    }

    [Fact]
    public async Task ChangeSector_Should_ReturnForbidden_WhenCallerLacksUpdatePermission()
    {
        // Arrange
        string token = NewToken();
        Guid itSectorId = await InsertSectorAsync("TI", $"Tecnologia-{token}");
        Guid anaId = await InsertUserAsync("Ana", "Souza", $"ana.{token}@example.com");
        AccessTokens agentTokens = await CreateAgentAsync();
        Authenticate(agentTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync($"users/{anaId}/sector", new { sectorId = itSectorId });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await GetSectorIdAsync(anaId)).ShouldBeNull();
    }

    [Fact]
    public async Task GetAttendants_Should_ReturnReadForbidden_WhenCallerManagesTicketsButLacksReadPermission()
    {
        // Arrange
        AccessTokens agentTokens = await CreateAgentAsync();
        Authenticate(agentTokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("users/attendants");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.ReadForbidden");
        problem.Detail.ShouldBe("Você não tem permissão para buscar usuários.");
    }

    [Fact]
    public async Task GetAttendants_Should_ReturnOnlyEligibleUsersByNameWithSixFields()
    {
        // Arrange
        AttendantScenario scenario = await CreateAttendantScenarioAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/attendants?searchTerm={scenario.Token}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedDto<AttendantDto>? page = await response.Content.ReadFromJsonAsync<PagedDto<AttendantDto>>();
        page!.Items.Select(a => a.Id).ShouldBe([scenario.Ana, scenario.Beto, scenario.Caio, scenario.Davi, scenario.Eva]);
        page.TotalCount.ShouldBe(5);

        AttendantDto ana = page.Items[0];
        ana.FullName.ShouldBe("Ana Souza");
        ana.Email.ShouldBe($"ana.{scenario.Token}@example.com");
        ana.SectorId.ShouldBe(scenario.ItSectorId);
        ana.SectorName.ShouldBe("TI");
        ana.DepartmentName.ShouldBe($"Tecnologia-{scenario.Token}");

        AttendantDto eva = page.Items[4];
        eva.SectorId.ShouldBeNull();
        eva.SectorName.ShouldBeNull();
        eva.DepartmentName.ShouldBeNull();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("items")[0].EnumerateObject().Select(p => p.Name)
            .ShouldBe(["id", "fullName", "email", "sectorId", "sectorName", "departmentName"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetAttendants_Should_FollowManagePermissionOfEachRole()
    {
        // Arrange
        AttendantScenario scenario = await CreateAttendantScenarioAsync();

        // Act: a role dos atendentes perde tickets:manage
        (await HttpClient.PutAsJsonAsync(
            $"roles/{scenario.AttendantRole}/permissions",
            new { permissionCodes = new[] { PermissionCodes.Tickets.Create } })).EnsureSuccessStatusCode();
        List<Guid> withoutAttendantRole = await GetAttendantIdsAsync($"searchTerm={scenario.Token}");

        // Act: a role dos colaboradores ganha tickets:manage
        (await HttpClient.PutAsJsonAsync(
            $"roles/{scenario.CollaboratorRole}/permissions",
            new { permissionCodes = new[] { PermissionCodes.Tickets.Create, PermissionCodes.Tickets.Manage } }))
            .EnsureSuccessStatusCode();
        List<Guid> withCollaboratorRole = await GetAttendantIdsAsync($"searchTerm={scenario.Token}");

        // Assert
        withoutAttendantRole.ShouldBe([scenario.Davi]);
        withCollaboratorRole.ShouldBe([scenario.Carla, scenario.Davi]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(101)]
    public async Task GetAttendants_Should_ReturnBadRequest_WhenSearchTermLengthIsOutOfRange(int length)
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users/attendants?searchTerm={new string('a', length)}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAttendants_Should_MatchEveryWordWithoutCaseInAnyField()
    {
        // Arrange
        AttendantScenario scenario = await CreateAttendantScenarioAsync();
        string token = scenario.Token;

        // Act
        List<Guid> upperCase = await GetAttendantIdsAsync($"searchTerm={Uri.EscapeDataString($"ANA {token}")}");
        List<Guid> department = await GetAttendantIdsAsync($"searchTerm={Uri.EscapeDataString($"tecnolog {token}")}");
        List<Guid> fullName = await GetAttendantIdsAsync($"searchTerm={Uri.EscapeDataString($"ana souza {token}")}");
        List<Guid> wordsOfDifferentUsers = await GetAttendantIdsAsync($"searchTerm={Uri.EscapeDataString($"souza lima {token}")}");
        List<Guid> likeWildcards = await GetAttendantIdsAsync($"searchTerm={Uri.EscapeDataString($"%% {token}")}");

        // Assert
        upperCase.ShouldBe([scenario.Ana]);
        department.ShouldBe([scenario.Ana, scenario.Beto]);
        fullName.ShouldBe([scenario.Ana]);
        wordsOfDifferentUsers.ShouldBeEmpty();
        likeWildcards.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAttendants_Should_AdjustPageSizeWithoutError_WhenPagingIsMissingOrOutOfRange()
    {
        // Arrange: 120 atendentes com o mesmo marcador no e-mail
        await AuthenticateAsAdminAsync();
        string token = NewToken();
        string attendantRole = await CreateRoleWithPermissionsAsync(PermissionCodes.Tickets.Manage);

        using (IServiceScope scope = Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            for (int i = 0; i < 120; i++)
            {
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = $"attendant{i}.{token}@example.com",
                    FirstName = $"Attendant{i:D3}",
                    LastName = "Test",
                    Code = UserCodes.New(),
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                context.Users.Add(user);
                context.UserRoles.Add(new UserRole { Id = Guid.NewGuid(), UserId = user.Id, RoleName = attendantRole });
            }

            await context.SaveChangesAsync();
        }

        // Act
        PagedDto<AttendantDto> withoutPageSize = await GetAttendantsPageAsync($"searchTerm={token}");
        PagedDto<AttendantDto> fifty = await GetAttendantsPageAsync($"searchTerm={token}&pageSize=50");
        PagedDto<AttendantDto> aboveMaximum = await GetAttendantsPageAsync($"searchTerm={token}&pageSize=500");
        PagedDto<AttendantDto> pageZero = await GetAttendantsPageAsync($"searchTerm={token}&page=0");

        // Assert
        withoutPageSize.Items.Count.ShouldBe(20);
        fifty.Items.Count.ShouldBe(50);
        aboveMaximum.Items.Count.ShouldBe(100);
        pageZero.Page.ShouldBe(1);
        pageZero.Items.Count.ShouldBe(20);
        new[] { withoutPageSize, fifty, aboveMaximum, pageZero }.ShouldAllBe(page => page.TotalCount == 120);
    }

    [Fact]
    public async Task GetAttendants_Should_PutPrioritySectorFirst()
    {
        // Arrange
        AttendantScenario scenario = await CreateAttendantScenarioAsync();

        // Act
        List<Guid> ids = await GetAttendantIdsAsync(
            $"searchTerm={scenario.Token}&prioritySectorId={scenario.FinanceSectorId}");

        // Assert
        ids.ShouldBe([scenario.Caio, scenario.Ana, scenario.Beto, scenario.Davi, scenario.Eva]);
    }

    [Fact]
    public async Task GetAttendants_Should_ReturnEmptyPage_WhenFiltersMatchNobody()
    {
        // Arrange
        AttendantScenario scenario = await CreateAttendantScenarioAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"users/attendants?searchTerm={Uri.EscapeDataString($"caio {scenario.Token}")}&sectorId={scenario.ItSectorId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedDto<AttendantDto>? page = await response.Content.ReadFromJsonAsync<PagedDto<AttendantDto>>();
        page!.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetAttendantsAndAssign_Should_IgnoreCachedPermissions_WhenRoleLosesManagePermission()
    {
        // Arrange: Beto atende chamados e já tem as permissões em cache
        AttendantScenario scenario = await CreateAttendantScenarioAsync();
        string betoEmail = $"beto2.{scenario.Token}@example.com";
        HttpResponseMessage registerResponse = await PostRegisterAsAdminAsync(new
        {
            email = betoEmail,
            firstName = "Beto",
            lastName = "Lima",
            password = "Password123!"
        });
        registerResponse.EnsureSuccessStatusCode();
        Guid betoId = await registerResponse.Content.ReadFromJsonAsync<Guid>();
        (await HttpClient.PutAsJsonAsync($"users/{betoId}/roles", new { roleNames = new[] { scenario.AttendantRole } }))
            .EnsureSuccessStatusCode();

        AccessTokens betoTokens = await LoginAsync(betoEmail);
        Authenticate(betoTokens.AccessToken);
        (await HttpClient.GetAsync("users/me")).EnsureSuccessStatusCode();

        await AuthenticateAsAdminAsync();
        (await GetAttendantIdsAsync($"searchTerm={Uri.EscapeDataString(betoEmail)}")).ShouldBe([betoId]);
        Guid ticketId = await CreateTicketAsync(scenario.ItSectorId);

        // Act
        (await HttpClient.PutAsJsonAsync(
            $"roles/{scenario.AttendantRole}/permissions",
            new { permissionCodes = new[] { PermissionCodes.Tickets.Create } })).EnsureSuccessStatusCode();
        List<Guid> attendants = await GetAttendantIdsAsync($"searchTerm={scenario.Token}");
        HttpResponseMessage assignResponse = await HttpClient.PutAsJsonAsync(
            $"tickets/{ticketId}/assignee",
            new { assignedToUserId = betoId });

        // Assert
        attendants.ShouldNotContain(betoId);
        assignResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await assignResponse.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Tickets.AssigneeNotAttendant");
    }

    [Fact]
    public void GetAttendants_Should_HaveNoRateLimitPolicyOfItsOwn()
    {
        // Arrange
        EndpointDataSource endpoints = Services.GetRequiredService<EndpointDataSource>();

        // Act
        RouteEndpoint endpoint = endpoints.Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "users/attendants");

        // Assert
        endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>().ShouldBeNull();
        endpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>().ShouldBeNull();
    }

    [Fact]
    public async Task GetAttendants_Should_ReturnTooManyRequests_WhenGlobalLimitIsExceeded()
    {
        // Arrange: a API com o limite global padrão (100 requisições por 60 s por usuário)
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        using var limitedFactory = new WebApplicationFactoryWithSetting(_factory, "RateLimiting:Global:PermitLimit", "100");
        using HttpClient client = limitedFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var statusCodes = new List<HttpStatusCode>();

        // Act
        for (int i = 0; i < 101; i++)
        {
            using HttpResponseMessage response = await client.GetAsync("users/attendants?pageSize=1");
            statusCodes.Add(response.StatusCode);
        }

        // Assert
        statusCodes.Take(100).ShouldAllBe(statusCode => statusCode == HttpStatusCode.OK);
        statusCodes[100].ShouldBe(HttpStatusCode.TooManyRequests);
    }

    private sealed record AttendantDto(
        Guid Id,
        string FullName,
        string Email,
        Guid? SectorId,
        string? SectorName,
        string? DepartmentName);

    /// <summary>
    /// Usuários dos critérios da spec 009, com um marcador único no e-mail: as buscas pelo marcador isolam o cenário
    /// dos usuários de outros testes. As roles do cenário são de teste, no papel de Atendente e Colaborador, porque as
    /// roles fixas são compartilhadas por todos os testes; o Davi tem uma role de teste no papel de Administrador, porque
    /// outros testes contam com um único administrador ativo.
    /// </summary>
    private sealed record AttendantScenario(
        string Token,
        Guid ItSectorId,
        Guid FinanceSectorId,
        string AttendantRole,
        string CollaboratorRole,
        Guid Ana,
        Guid Beto,
        Guid Caio,
        Guid Bruno,
        Guid Carla,
        Guid Davi,
        Guid Eva);

    /// <summary>
    /// Monta o <see cref="AttendantScenario"/> e deixa o administrador autenticado.
    /// </summary>
    private async Task<AttendantScenario> CreateAttendantScenarioAsync()
    {
        await AuthenticateAsAdminAsync();
        string token = NewToken();

        string attendantRole = await CreateRoleWithPermissionsAsync(PermissionCodes.Tickets.Create, PermissionCodes.Tickets.Manage);
        string collaboratorRole = await CreateRoleWithPermissionsAsync(PermissionCodes.Tickets.Create);
        string administratorRole = await CreateRoleWithPermissionsAsync(PermissionCodes.Tickets.Manage, PermissionCodes.Tickets.Assign);
        Guid itSectorId = await InsertSectorAsync("TI", $"Tecnologia-{token}");
        Guid financeSectorId = await InsertSectorAsync("Financeiro", $"Controladoria-{token}");

        return new AttendantScenario(
            token,
            itSectorId,
            financeSectorId,
            attendantRole,
            collaboratorRole,
            Ana: await InsertUserAsync("Ana", "Souza", $"ana.{token}@example.com", itSectorId, true, attendantRole),
            Beto: await InsertUserAsync("Beto", "Lima", $"beto.{token}@example.com", itSectorId, true, attendantRole),
            Caio: await InsertUserAsync("Caio", "Melo", $"caio.{token}@example.com", financeSectorId, true, attendantRole),
            Bruno: await InsertUserAsync("Bruno", "Alves", $"bruno.{token}@example.com", itSectorId, false, attendantRole),
            Carla: await InsertUserAsync("Carla", "Dias", $"carla.{token}@example.com", itSectorId, true, collaboratorRole),
            Davi: await InsertUserAsync("Davi", "Rocha", $"davi.{token}@example.com", null, true, administratorRole),
            Eva: await InsertUserAsync("Eva", "Prado", $"eva.{token}@example.com", null, true, attendantRole));
    }

    /// <summary>
    /// Marcador único do cenário, só com caracteres hexadecimais: não contém nenhum nome nem palavra buscada.
    /// </summary>
    private static string NewToken() => Guid.NewGuid().ToString("N")[..12];

    private async Task AuthenticateAsAdminAsync()
    {
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);
    }

    /// <summary>
    /// Usuário novo com a role Atendente (que tem <c>tickets:manage</c> e não tem <c>users:read</c>).
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

    private async Task<PagedDto<AttendantDto>> GetAttendantsPageAsync(string queryString)
    {
        HttpResponseMessage response = await HttpClient.GetAsync($"users/attendants?{queryString}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<PagedDto<AttendantDto>>())!;
    }

    private async Task<List<Guid>> GetAttendantIdsAsync(string queryString) =>
        (await GetAttendantsPageAsync(queryString)).Items.Select(a => a.Id).ToList();

    private async Task<Guid?> GetSectorIdAsync(Guid userId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.Users.Where(u => u.Id == userId).Select(u => u.SectorId).SingleAsync();
    }

    [Fact]
    public async Task GetAll_Should_ReturnSectorFields_ForUsersWithAndWithoutSector()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string token = NewToken();
        Guid itSectorId = await InsertSectorAsync("TI", $"Tecnologia-{token}");
        Guid anaId = await InsertUserAsync("Ana", "Souza", $"ana.{token}@example.com", itSectorId);
        Guid evaId = await InsertUserAsync("Eva", "Prado", $"eva.{token}@example.com");

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users?search={token}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PagedDto<UserSectorDto>? page = await response.Content.ReadFromJsonAsync<PagedDto<UserSectorDto>>();
        page!.TotalCount.ShouldBe(2);
        page.Items.Select(u => u.Id).ShouldBe([anaId, evaId]);
        page.Items[0].ShouldBe(new UserSectorDto(anaId, itSectorId, "TI", $"Tecnologia-{token}", true));
        page.Items[1].ShouldBe(new UserSectorDto(evaId, null, null, null, null));
    }

    [Fact]
    public async Task GetById_Should_ReturnSectorFields_WhenCallerIsAdminOrSelf()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string token = NewToken();
        Guid itSectorId = await InsertSectorAsync("TI", $"Tecnologia-{token}");
        string anaEmail = $"ana.{token}@example.com";
        Guid anaId = await RegisterUserAsync(anaEmail);
        (await HttpClient.PutAsJsonAsync($"users/{anaId}/sector", new { sectorId = itSectorId })).EnsureSuccessStatusCode();
        var expected = new UserSectorDto(anaId, itSectorId, "TI", $"Tecnologia-{token}", true);
        AccessTokens agentTokens = await CreateAgentAsync();

        // Act
        await AuthenticateAsAdminAsync();
        UserSectorDto? asAdmin = await HttpClient.GetFromJsonAsync<UserSectorDto>($"users/{anaId}");
        Authenticate((await LoginAsync(anaEmail)).AccessToken);
        UserSectorDto? asSelf = await HttpClient.GetFromJsonAsync<UserSectorDto>($"users/{anaId}");
        Authenticate(agentTokens.AccessToken);
        HttpResponseMessage asAgent = await HttpClient.GetAsync($"users/{anaId}");

        // Assert
        asAdmin.ShouldBe(expected);
        asSelf.ShouldBe(expected);
        asAgent.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllAndGetById_Should_ReturnInactiveSector_WhenSectorIsDeactivated()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string token = NewToken();
        Guid itSectorId = await InsertSectorAsync("TI", $"Tecnologia-{token}");
        Guid anaId = await InsertUserAsync("Ana", "Souza", $"ana.{token}@example.com", itSectorId);
        (await HttpClient.DeleteAsync($"sectors/{itSectorId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var expected = new UserSectorDto(anaId, itSectorId, "TI", $"Tecnologia-{token}", false);

        // Act
        PagedDto<UserSectorDto>? page = await HttpClient.GetFromJsonAsync<PagedDto<UserSectorDto>>($"users?search={token}");
        UserSectorDto? byId = await HttpClient.GetFromJsonAsync<UserSectorDto>($"users/{anaId}");

        // Assert
        page!.Items.ShouldHaveSingleItem().ShouldBe(expected);
        byId.ShouldBe(expected);
    }

    [Fact]
    public async Task GetAllAndGetById_Should_FollowSectorChanges()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string token = NewToken();
        Guid itSectorId = await InsertSectorAsync("TI", $"Tecnologia-{token}");
        Guid evaId = await InsertUserAsync("Eva", "Prado", $"eva.{token}@example.com");
        var withSector = new UserSectorDto(evaId, itSectorId, "TI", $"Tecnologia-{token}", true);
        var withoutSector = new UserSectorDto(evaId, null, null, null, null);

        // Act
        (await HttpClient.PutAsJsonAsync($"users/{evaId}/sector", new { sectorId = itSectorId })).EnsureSuccessStatusCode();
        UserSectorDto afterSetInList = (await HttpClient.GetFromJsonAsync<PagedDto<UserSectorDto>>($"users?search={token}"))!
            .Items.Single();
        UserSectorDto? afterSetById = await HttpClient.GetFromJsonAsync<UserSectorDto>($"users/{evaId}");

        (await HttpClient.PutAsJsonAsync($"users/{evaId}/sector", new { sectorId = (Guid?)null })).EnsureSuccessStatusCode();
        UserSectorDto afterRemoveInList = (await HttpClient.GetFromJsonAsync<PagedDto<UserSectorDto>>($"users?search={token}"))!
            .Items.Single();
        UserSectorDto? afterRemoveById = await HttpClient.GetFromJsonAsync<UserSectorDto>($"users/{evaId}");

        // Assert
        afterSetInList.ShouldBe(withSector);
        afterSetById.ShouldBe(withSector);
        afterRemoveInList.ShouldBe(withoutSector);
        afterRemoveById.ShouldBe(withoutSector);
    }

    [Fact]
    public async Task GetCurrentAndGetByEmail_Should_NotExposeSectorFields()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string token = NewToken();
        Guid itSectorId = await InsertSectorAsync("TI", $"Tecnologia-{token}");
        string anaEmail = $"ana.{token}@example.com";
        Guid anaId = await RegisterUserAsync(anaEmail);
        (await HttpClient.PutAsJsonAsync($"users/{anaId}/sector", new { sectorId = itSectorId })).EnsureSuccessStatusCode();

        // Act
        string byEmail = await HttpClient.GetStringAsync($"users/by-email?email={Uri.EscapeDataString(anaEmail)}");
        Authenticate((await LoginAsync(anaEmail)).AccessToken);
        string current = await HttpClient.GetStringAsync("users/me");

        // Assert
        PropertyNames(byEmail).ShouldNotContain(name => SectorPropertyNames.Contains(name));
        PropertyNames(current).ShouldNotContain(name => SectorPropertyNames.Contains(name));
    }

    /// <summary>
    /// Os campos de setor que só `GET users` e `GET users/{id}` expõem (spec 013).
    /// </summary>
    private static readonly string[] SectorPropertyNames = ["sectorId", "sectorName", "departmentName", "sectorIsActive"];

    /// <summary>
    /// Nomes das propriedades do objeto JSON na raiz da resposta.
    /// </summary>
    private static List<string> PropertyNames(string json)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.EnumerateObject().Select(property => property.Name).ToList();
    }

    /// <summary>
    /// Id e campos de setor de um usuário, como `GET users` (cada item) e `GET users/{id}` devolvem.
    /// </summary>
    private sealed record UserSectorDto(
        Guid Id,
        Guid? SectorId,
        string? SectorName,
        string? DepartmentName,
        bool? SectorIsActive);
}
