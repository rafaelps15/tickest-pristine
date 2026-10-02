using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using TickestPristine.Application.Authorization;

namespace TickestPristine.IntegrationTests.Users;

public sealed class UsersTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string AdminEmail = "admin@tickestpristine.dev";
    private const string AdminPassword = "ChangeMe123!";

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
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);

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
    public async Task GetAll_Should_ReturnAssignedRolesForEachUser()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        HttpResponseMessage createRoleResponse = await HttpClient.PostAsJsonAsync("roles", new { name = $"Role-{Guid.NewGuid():N}" });
        createRoleResponse.EnsureSuccessStatusCode();
        Guid roleId = await createRoleResponse.Content.ReadFromJsonAsync<Guid>();

        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        HttpResponseMessage assignResponse = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/roles",
            new { roleIds = new[] { roleId } });
        assignResponse.EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"users?search={email}");

        // Assert
        response.EnsureSuccessStatusCode();
        PagedDto<UserSummaryDto>? users = await response.Content.ReadFromJsonAsync<PagedDto<UserSummaryDto>>();
        UserSummaryDto user = users!.Items.Single(u => u.Id == userId);
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
        me!.Permissions.ShouldBe(PermissionCodes.All, ignoreOrder: true);
    }

    [Fact]
    public async Task GetCurrent_Should_ReturnBadRequest_WhenUserWasDeactivatedAfterLogin()
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
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.Deactivated");
    }

    [Fact]
    public async Task GetCurrent_Should_ReturnUnauthorized_WhenNotAuthenticated()
    {
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

        HttpResponseMessage createRoleResponse = await HttpClient.PostAsJsonAsync("roles", new { name = $"Role-{Guid.NewGuid():N}" });
        createRoleResponse.EnsureSuccessStatusCode();
        Guid roleId = await createRoleResponse.Content.ReadFromJsonAsync<Guid>();

        Guid userId = await RegisterUserAsync(UniqueEmail());

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/roles",
            new { roleIds = new[] { roleId } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AssignRoles_Should_ReturnProblem_WhenRoleDoesNotExist()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        Guid userId = await RegisterUserAsync(UniqueEmail());

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/roles",
            new { roleIds = new[] { Guid.NewGuid() } });

        // Assert
        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Fact]
    public async Task AssignRoles_Should_ReturnForbidden_WhenRoleGrantsAdministrativePermissionCallerLacks()
    {
        // Arrange: role que só atribui roles, dada a um usuário comum
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        Guid delegateRoleId = await CreateRoleWithPermissionsAsync("users:assign-roles");
        Guid privilegedRoleId = await CreateRoleWithPermissionsAsync("roles:manage");
        Guid ticketRoleId = await CreateRoleWithPermissionsAsync("tickets:manage");

        string delegateEmail = UniqueEmail();
        Guid delegateUserId = await RegisterUserAsync(delegateEmail);
        (await HttpClient.PutAsJsonAsync($"users/{delegateUserId}/roles", new { roleIds = new[] { delegateRoleId } }))
            .EnsureSuccessStatusCode();

        Guid targetUserId = await RegisterUserAsync(UniqueEmail());

        AccessTokens delegateTokens = await LoginAsync(delegateEmail);
        Authenticate(delegateTokens.AccessToken);

        // Act
        HttpResponseMessage escalation = await HttpClient.PutAsJsonAsync(
            $"users/{targetUserId}/roles",
            new { roleIds = new[] { privilegedRoleId } });

        HttpResponseMessage ticketOnly = await HttpClient.PutAsJsonAsync(
            $"users/{targetUserId}/roles",
            new { roleIds = new[] { ticketRoleId } });

        // Assert
        escalation.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        ticketOnly.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AssignRoles_Should_ReturnForbidden_WhenCallerLacksManageRolesPermission()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.PutAsJsonAsync(
            $"users/{userId}/roles",
            new { roleIds = Array.Empty<Guid>() });

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
            new { roleIds = Array.Empty<Guid>() });

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
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? loginProblem = await loginResponse.Content.ReadFromJsonAsync<ProblemDto>();
        loginProblem!.Title.ShouldBe("Users.Deactivated");

        HttpResponseMessage refreshResponse = await HttpClient.PostAsJsonAsync(
            "users/refresh-token",
            new { refreshToken = userTokens.RefreshToken });
        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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
    public async Task Activate_Should_ReturnUnauthorized_WhenNotAuthenticated()
    {
        // Arrange
        Guid userId = await RegisterUserAsync(UniqueEmail());

        // Act
        HttpResponseMessage response = await HttpClient.PutAsync($"users/{userId}/activate", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<Guid> CreateRoleWithPermissionsAsync(params string[] permissionCodes)
    {
        HttpResponseMessage createRoleResponse = await HttpClient.PostAsJsonAsync("roles", new { name = $"Role-{Guid.NewGuid():N}" });
        createRoleResponse.EnsureSuccessStatusCode();
        Guid roleId = await createRoleResponse.Content.ReadFromJsonAsync<Guid>();

        (await HttpClient.PutAsJsonAsync($"roles/{roleId}/permissions", new { permissionCodes }))
            .EnsureSuccessStatusCode();

        return roleId;
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

    private sealed record RoleSummaryDto(Guid Id, string Name);

    private sealed record UserByEmailDto(Guid Id, string Email, string FirstName, string LastName);

    private sealed record ProblemDto(int Status, string Title, string Detail);

    private sealed record CurrentUserDto(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        List<RoleSummaryDto> Roles,
        List<string> Permissions);
}
