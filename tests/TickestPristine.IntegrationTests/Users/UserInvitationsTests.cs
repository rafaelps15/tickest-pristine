using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Application.Abstractions.Notifications;
using TickestPristine.Domain.Audit;
using TickestPristine.Domain.Users;
using TickestPristine.Infrastructure.Database;
using TickestPristine.IntegrationTests.Tickets;

namespace TickestPristine.IntegrationTests.Users;

/// <summary>
/// Convite por e-mail e ativação da conta pelo próprio usuário (spec 008).
/// </summary>
public sealed partial class UserInvitationsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string StrongPassword = "Password123!";
    private const string InvalidTokenDetail = "Link inválido ou expirado. Solicite um novo convite ao administrador.";

    private readonly IntegrationTestWebAppFactory _factory = factory;

    [Fact]
    public async Task Register_Should_CreatePendingUserWithInvitationAndNoCredential()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string email = UniqueEmail();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/register",
            new { email, firstName = "Ana", lastName = "Souza" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Guid userId = await response.Content.ReadFromJsonAsync<Guid>();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await context.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        user.IsActive.ShouldBeTrue();
        (await context.UserRoles.SingleAsync(r => r.UserId == userId)).RoleName.ShouldBe("Colaborador");
        (await context.UserCredentials.AnyAsync(c => c.UserId == userId)).ShouldBeFalse();
        UserInvitation invitation = await context.UserInvitations.AsNoTracking().SingleAsync(i => i.UserId == userId);
        invitation.ExpiresAtUtc.ShouldBe(invitation.CreatedAtUtc.AddHours(72));
    }

    [Fact]
    public async Task Register_Should_IgnorePasswordFieldAndCreateNoCredential()
    {
        // Arrange
        await AuthenticateAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/register",
            new { email = UniqueEmail(), firstName = "Ana", lastName = "Souza", password = StrongPassword });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Guid userId = await response.Content.ReadFromJsonAsync<Guid>();
        (await HasCredentialAsync(userId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Invitation_Should_StoreOnlySha256OfTokenAndKeepItOutOfAudit()
    {
        // Arrange
        (Guid userId, string email) = await RegisterPendingUserAsync();
        string token = EmailSender.LatestTokenFor(email);

        // Act
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserInvitation invitation = await context.UserInvitations.AsNoTracking().SingleAsync(i => i.UserId == userId);
        List<AuditLogEntry> audit = await context.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(UserInvitation) && a.EntityId == invitation.Id.ToString())
            .ToListAsync();

        // Assert
        Sha256HexRegex().IsMatch(invitation.TokenHash).ShouldBeTrue();
        invitation.TokenHash.ShouldNotContain(token);
        invitation.TokenHash.ShouldBe(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))));
        audit.ShouldNotBeEmpty();
        audit.ShouldAllBe(a => !Contains(a, invitation.TokenHash) && !Contains(a, token) && !Contains(a, "TokenHash"));
    }

    [Fact]
    public async Task Register_Should_SendInvitationEmailWithNameCodeLinkAndExpiration()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        string email = UniqueEmail();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/register",
            new { email, firstName = "Ana", lastName = "Souza" });

        // Assert
        Guid userId = await response.Content.ReadFromJsonAsync<Guid>();
        EmailMessage message = EmailSender.MessagesTo(email).ShouldHaveSingleItem();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string code = await context.Users.Where(u => u.Id == userId).Select(u => u.Code).SingleAsync();
        UserInvitation invitation = await context.UserInvitations.AsNoTracking().SingleAsync(i => i.UserId == userId);
        string token = CapturingEmailSender.ExtractToken(message.TextBody);

        message.TextBody.ShouldContain("Olá, Ana!");
        message.TextBody.ShouldContain(code);
        message.TextBody.ShouldContain(email);
        message.TextBody.ShouldContain($"https://tickest.test/ativar-conta#token={Uri.EscapeDataString(token)}");
        message.TextBody.ShouldContain(FormatBrasiliaTime(invitation.ExpiresAtUtc));
        PasswordValueRegex().IsMatch(message.TextBody).ShouldBeFalse();
        PasswordValueRegex().IsMatch(message.HtmlBody).ShouldBeFalse();
        message.TextBody.ShouldNotContain(invitation.TokenHash);
        message.HtmlBody.ShouldNotContain(invitation.TokenHash);
    }

    [Fact]
    public async Task Register_Should_KeepUserAndInvitation_WhenEmailSendingFails()
    {
        // Arrange: o envio para este endereço falha (CapturingEmailSender)
        await AuthenticateAsAdminAsync();
        string email = $"{CapturingEmailSender.FailingAddressMarker}-{Guid.NewGuid():N}@example.com";

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/register",
            new { email, firstName = "Ana", lastName = "Souza" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Guid userId = await response.Content.ReadFromJsonAsync<Guid>();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Users.AnyAsync(u => u.Id == userId)).ShouldBeTrue();
        (await context.UserInvitations.AnyAsync(i => i.UserId == userId && i.RevokedAtUtc == null)).ShouldBeTrue();
    }

    [Fact]
    public async Task ResendInvitation_Should_RevokePreviousIssueNewOneAndSendEmail()
    {
        // Arrange
        (Guid userId, string email) = await RegisterPendingUserAsync();
        string oldToken = EmailSender.LatestTokenFor(email);

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync($"users/{userId}/invitation", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        EmailSender.MessagesTo(email).Count.ShouldBe(2);

        List<UserInvitation> invitations = await GetInvitationsAsync(userId);
        invitations.Count.ShouldBe(2);
        invitations.Count(i => i.RevokedAtUtc != null).ShouldBe(1);
        invitations.Single(i => i.RevokedAtUtc == null).TokenHash
            .ShouldBe(Sha256Hex(EmailSender.LatestTokenFor(email)));

        await ShouldBeInvalidTokenAsync(await ActivateAccountAsync(oldToken, StrongPassword));
    }

    [Fact]
    public async Task ResendInvitation_Should_RejectActivatedDeactivatedAndMissingUsers()
    {
        // Arrange
        Guid activatedId = await RegisterUserAsync(UniqueEmail());
        (Guid deactivatedId, _) = await RegisterPendingUserAsync();
        (await HttpClient.PutAsync($"users/{deactivatedId}/deactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage activated = await HttpClient.PostAsync($"users/{activatedId}/invitation", null);
        HttpResponseMessage deactivated = await HttpClient.PostAsync($"users/{deactivatedId}/invitation", null);
        HttpResponseMessage missing = await HttpClient.PostAsync($"users/{Guid.NewGuid()}/invitation", null);

        // Assert
        activated.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await activated.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Users.AlreadyActivated");
        deactivated.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await deactivated.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Users.InvitationUserDeactivated");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResendInvitation_Should_ReturnLimitReached_AfterFiveInvitationsIn24Hours()
    {
        // Arrange: o cadastro emite o 1º convite; quatro reenvios completam cinco
        (Guid userId, string email) = await RegisterPendingUserAsync();
        for (int i = 0; i < 4; i++)
        {
            (await HttpClient.PostAsync($"users/{userId}/invitation", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        // Act
        HttpResponseMessage response = await HttpClient.PostAsync($"users/{userId}/invitation", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<ProblemDto>())!.Title.ShouldBe("Users.InvitationLimitReached");
        (await GetInvitationsAsync(userId)).Count.ShouldBe(5);
        EmailSender.MessagesTo(email).Count.ShouldBe(5);
    }

    [Fact]
    public async Task Activation_Should_CreateCredentialMarkInvitationUsedAndAllowLogin()
    {
        // Arrange
        (Guid userId, string email) = await RegisterPendingUserAsync();
        string token = EmailSender.LatestTokenFor(email);

        // Act
        HttpResponseMessage response = await ActivateAccountAsync(token, StrongPassword);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserCredential credential = await context.UserCredentials.AsNoTracking().SingleAsync(c => c.UserId == userId);
        credential.MustChangePassword.ShouldBeFalse();
        (await context.UserInvitations.AsNoTracking().SingleAsync(i => i.UserId == userId)).UsedAtUtc.ShouldNotBeNull();

        AccessTokens tokens = await LoginAsync(email, StrongPassword);
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Activation_Should_ReturnSameError_ForExpiredUsedRevokedUnknownAndDeactivatedUserTokens()
    {
        // Arrange
        (Guid expiredUserId, string expiredEmail) = await RegisterPendingUserAsync();
        await ExecuteAsync(context => context.UserInvitations
            .Where(i => i.UserId == expiredUserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1))));

        (_, string usedEmail) = await RegisterPendingUserAsync();
        string usedToken = EmailSender.LatestTokenFor(usedEmail);
        (await ActivateAccountAsync(usedToken, StrongPassword)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (Guid revokedUserId, string revokedEmail) = await RegisterPendingUserAsync();
        string revokedToken = EmailSender.LatestTokenFor(revokedEmail);
        (await HttpClient.PostAsync($"users/{revokedUserId}/invitation", null)).EnsureSuccessStatusCode();

        (Guid deactivatedUserId, string deactivatedEmail) = await RegisterPendingUserAsync();
        await ExecuteAsync(context => context.Users
            .Where(u => u.Id == deactivatedUserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.IsActive, false)));

        // Act
        HttpResponseMessage[] responses =
        [
            await ActivateAccountAsync(EmailSender.LatestTokenFor(expiredEmail), StrongPassword),
            await ActivateAccountAsync(usedToken, StrongPassword),
            await ActivateAccountAsync(revokedToken, StrongPassword),
            await ActivateAccountAsync("token-que-nao-existe", StrongPassword),
            await ActivateAccountAsync(EmailSender.LatestTokenFor(deactivatedEmail), StrongPassword)
        ];

        // Assert
        foreach (HttpResponseMessage response in responses)
        {
            await ShouldBeInvalidTokenAsync(response);
        }

        (await HasCredentialAsync(expiredUserId)).ShouldBeFalse();
        (await HasCredentialAsync(revokedUserId)).ShouldBeFalse();
        (await HasCredentialAsync(deactivatedUserId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Activation_Should_RejectWeakPasswordAndKeepInvitationUsable()
    {
        // Arrange
        (Guid userId, string email) = await RegisterPendingUserAsync();
        string token = EmailSender.LatestTokenFor(email);

        // Act
        HttpResponseMessage weak = await ActivateAccountAsync(token, "abc");
        HttpResponseMessage strong = await ActivateAccountAsync(token, StrongPassword);

        // Assert
        weak.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        strong.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await HasCredentialAsync(userId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Activation_Should_CreateSingleCredential_WhenTwoActivationsRunAtOnce()
    {
        // Arrange
        (Guid userId, string email) = await RegisterPendingUserAsync();
        string token = EmailSender.LatestTokenFor(email);

        // Gatilho temporário que segura por um segundo o uso do convite, com a linha bloqueada: a outra ativação lê o
        // mesmo convite nesse intervalo e, ao gravar, encontra outra versão ou a credencial já criada.
        await ExecuteSqlAsync(DelayInvitationUseSql);

        HttpResponseMessage[] responses;

        try
        {
            // Act
            responses = await Task.WhenAll(
                ActivateAccountAsync(token, StrongPassword),
                ActivateAccountAsync(token, StrongPassword));
        }
        finally
        {
            await ExecuteSqlAsync(DropDelayInvitationUseSql);
        }

        // Assert
        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
        responses.Single(r => r.StatusCode != HttpStatusCode.NoContent).StatusCode
            .ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.UserCredentials.CountAsync(c => c.UserId == userId)).ShouldBe(1);
    }

    [Fact]
    public async Task Login_Should_ReturnInvalidCredentials_WhenAccountIsPendingActivation()
    {
        // Arrange
        (_, string email) = await RegisterPendingUserAsync();

        // Act
        HttpResponseMessage pending = await HttpClient.PostAsJsonAsync("users/login", new { email, password = StrongPassword });
        HttpResponseMessage unknown = await HttpClient.PostAsJsonAsync(
            "users/login",
            new { email = UniqueEmail(), password = StrongPassword });

        // Assert
        pending.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? pendingProblem = await pending.Content.ReadFromJsonAsync<ProblemDto>();
        pendingProblem!.Title.ShouldBe("Users.InvalidCredentials");
        pendingProblem.ShouldBe(await unknown.Content.ReadFromJsonAsync<ProblemDto>());
    }

    [Fact]
    public async Task Deactivation_Should_RevokeInvitation_AndReactivationKeepAccountPendingWithoutInvitation()
    {
        // Arrange
        (Guid userId, string email) = await RegisterPendingUserAsync();
        string token = EmailSender.LatestTokenFor(email);

        // Act
        (await HttpClient.PutAsync($"users/{userId}/deactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        List<UserInvitation> afterDeactivation = await GetInvitationsAsync(userId);
        (await HttpClient.PutAsync($"users/{userId}/activate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        UserPendingDto? user = await HttpClient.GetFromJsonAsync<UserPendingDto>($"users/{userId}");
        HttpResponseMessage activation = await ActivateAccountAsync(token, StrongPassword);

        // Assert
        afterDeactivation.ShouldHaveSingleItem().RevokedAtUtc.ShouldNotBeNull();
        user!.PendingActivation.ShouldBeTrue();
        user.InvitationExpiresAtUtc.ShouldBeNull();
        await ShouldBeInvalidTokenAsync(activation);
    }

    [Fact]
    public async Task GetByIdAndGetAll_Should_ShowPendingActivationAndInvitationExpiration()
    {
        // Arrange: os dois e-mails têm o mesmo marcador, para a listagem encontrar só eles
        string marker = Guid.NewGuid().ToString("N")[..12];
        (Guid pendingId, _) = await RegisterPendingUserAsync($"pendente.{marker}@example.com");
        Guid activatedId = await RegisterUserAsync($"ativado.{marker}@example.com");
        DateTime expiresAtUtc = (await GetInvitationsAsync(pendingId)).Single().ExpiresAtUtc;
        await AuthenticateAsAdminAsync();

        // Act
        UserPendingDto? pending = await HttpClient.GetFromJsonAsync<UserPendingDto>($"users/{pendingId}");
        UserPendingDto? activated = await HttpClient.GetFromJsonAsync<UserPendingDto>($"users/{activatedId}");
        PagedUsersDto? page = await HttpClient.GetFromJsonAsync<PagedUsersDto>($"users?search={marker}");

        // Assert
        pending!.PendingActivation.ShouldBeTrue();
        pending.InvitationExpiresAtUtc!.Value.ShouldBe(expiresAtUtc, TimeSpan.FromMilliseconds(1));
        activated!.PendingActivation.ShouldBeFalse();
        activated.InvitationExpiresAtUtc.ShouldBeNull();

        page!.Items.Single(u => u.Id == pendingId).PendingActivation.ShouldBeTrue();
        page.Items.Single(u => u.Id == pendingId).InvitationExpiresAtUtc!.Value.ShouldBe(expiresAtUtc, TimeSpan.FromMilliseconds(1));
        page.Items.Single(u => u.Id == activatedId).PendingActivation.ShouldBeFalse();
        page.Items.Single(u => u.Id == activatedId).InvitationExpiresAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Invitations_Should_BeAuditedOnIssueRevocationAndUse_WithoutTokenHash()
    {
        // Arrange
        (Guid userId, string email) = await RegisterPendingUserAsync();
        (await HttpClient.PostAsync($"users/{userId}/invitation", null)).EnsureSuccessStatusCode();
        (await ActivateAccountAsync(EmailSender.LatestTokenFor(email), StrongPassword)).EnsureSuccessStatusCode();

        // Act
        List<UserInvitation> invitations = await GetInvitationsAsync(userId);
        var invitationIds = invitations.Select(i => i.Id.ToString()).ToList();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<AuditLogEntry> audit = await context.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(UserInvitation) && invitationIds.Contains(a.EntityId))
            .ToListAsync();

        // Assert: duas inclusões (cadastro e reenvio) e duas alterações (revogação do 1º e uso do 2º)
        audit.Count(a => a.Action == "Added").ShouldBe(2);
        audit.Count(a => a.Action == "Modified").ShouldBe(2);
        audit.Single(a => a.Action == "Modified" && a.AffectedColumns == nameof(UserInvitation.RevokedAtUtc)).ShouldNotBeNull();
        audit.Single(a => a.Action == "Modified" && a.AffectedColumns == nameof(UserInvitation.UsedAtUtc)).ShouldNotBeNull();
        audit.ShouldAllBe(a => !Contains(a, "TokenHash") && invitations.All(i => !Contains(a, i.TokenHash)));
    }

    [Fact]
    public async Task Activation_Should_ReturnTooManyRequests_WhenAuthenticationLimitIsExceeded()
    {
        // Arrange: a API com o limite padrão da política de autenticação (10 por 60 s)
        using var limitedFactory = new WebApplicationFactoryWithSetting(_factory, "RateLimiting:Authentication:PermitLimit", "10");
        using HttpClient client = limitedFactory.CreateClient();
        var statusCodes = new List<HttpStatusCode>();

        // Act
        for (int i = 0; i < 11; i++)
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync(
                "users/activation",
                new { token = "token-que-nao-existe", newPassword = StrongPassword });
            statusCodes.Add(response.StatusCode);
        }

        // Assert
        statusCodes.Take(10).ShouldAllBe(statusCode => statusCode == HttpStatusCode.BadRequest);
        statusCodes[10].ShouldBe(HttpStatusCode.TooManyRequests);
    }

    private const string DelayInvitationUseSql =
        """
        CREATE OR REPLACE FUNCTION public.test_delay_invitation_use() RETURNS trigger AS $$
        BEGIN
            PERFORM pg_sleep(1);
            RETURN NEW;
        END;
        $$ LANGUAGE plpgsql;

        CREATE TRIGGER test_delay_invitation_use
            BEFORE UPDATE ON public.user_invitations
            FOR EACH ROW
            WHEN (OLD.used_at_utc IS DISTINCT FROM NEW.used_at_utc)
            EXECUTE FUNCTION public.test_delay_invitation_use();
        """;

    private const string DropDelayInvitationUseSql =
        """
        DROP TRIGGER IF EXISTS test_delay_invitation_use ON public.user_invitations;
        DROP FUNCTION IF EXISTS public.test_delay_invitation_use();
        """;

    private sealed record ProblemDto(int Status, string Title, string Detail);

    private sealed record UserPendingDto(Guid Id, bool PendingActivation, DateTime? InvitationExpiresAtUtc);

    private sealed record PagedUsersDto(List<UserPendingDto> Items, int TotalCount);

    [GeneratedRegex("^[0-9A-F]{64}$")]
    private static partial Regex Sha256HexRegex();

    /// <summary>
    /// "senha:" seguida de um valor, o que indicaria uma senha no corpo do e-mail.
    /// </summary>
    [GeneratedRegex(@"senha:\s*\S", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordValueRegex();

    private async Task AuthenticateAsAdminAsync()
    {
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);
    }

    /// <summary>
    /// Cadastra um usuário pelo administrador sem ativar a conta; deixa o administrador autenticado.
    /// </summary>
    private async Task<(Guid UserId, string Email)> RegisterPendingUserAsync(string? email = null)
    {
        await AuthenticateAsAdminAsync();
        email ??= UniqueEmail();

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/register",
            new { email, firstName = "Ana", lastName = "Souza" });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<Guid>(), email);
    }

    private static async Task ShouldBeInvalidTokenAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Users.InvalidActivationToken");
        problem.Detail.ShouldBe(InvalidTokenDetail);
    }

    private async Task<bool> HasCredentialAsync(Guid userId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.UserCredentials.AnyAsync(c => c.UserId == userId);
    }

    private async Task<List<UserInvitation>> GetInvitationsAsync(Guid userId)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.UserInvitations.AsNoTracking().Where(i => i.UserId == userId).ToListAsync();
    }

    private async Task ExecuteAsync(Func<ApplicationDbContext, Task<int>> action)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await action(context);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.ExecuteSqlRawAsync(sql);
    }

    private static bool Contains(AuditLogEntry entry, string value) =>
        (entry.OldValues?.Contains(value, StringComparison.Ordinal) ?? false) ||
        (entry.NewValues?.Contains(value, StringComparison.Ordinal) ?? false) ||
        (entry.AffectedColumns?.Contains(value, StringComparison.Ordinal) ?? false);

    private static string Sha256Hex(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>
    /// Mesmo formato do e-mail de convite: data e hora no horário de Brasília.
    /// </summary>
    private static string FormatBrasiliaTime(DateTime utc)
    {
        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));

        return local.ToString("dd/MM/yyyy 'às' HH:mm", CultureInfo.GetCultureInfo("pt-BR")) + " (horário de Brasília)";
    }
}
