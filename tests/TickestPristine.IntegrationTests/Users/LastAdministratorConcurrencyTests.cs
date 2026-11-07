using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Application.Roles;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Web.Api;

namespace TickestPristine.IntegrationTests.Users;

// Remoções de administrador pela API, cada teste num banco próprio: no banco compartilhado, o administrador do seed não
// pode perder o acesso, porque os outros testes dependem dele.
public sealed class LastAdministratorConcurrencyTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const int Rounds = 20;
    private const string Password = "Password123!";

    private readonly IntegrationTestWebAppFactory _factory = factory;

    [Fact]
    public async Task Deactivate_Should_KeepOneActiveAdministrator_WhenTwoAdministratorsDeactivateEachOtherConcurrently()
    {
        // Arrange: o administrador do seed e um segundo administrador, os dois com senha
        await using IsolatedApi api = await IsolatedApi.StartAsync(_factory, await CreateEmptyDatabaseAsync());
        (Guid otherId, string otherEmail) = await api.CreateAdministratorAsync();
        List<RoundOutcome> outcomes = [];

        // Act: em cada rodada, cada um desativa o outro ao mesmo tempo; quem ficou reativa o outro para a próxima
        for (int round = 0; round < Rounds; round++)
        {
            using HttpClient seed = await api.LoginAsync(AdminEmail, AdminPassword);
            using HttpClient other = await api.LoginAsync(otherEmail, Password);

            HttpResponseMessage[] responses = await Task.WhenAll(
                seed.PutAsync($"users/{otherId}/deactivate", null),
                other.PutAsync($"users/{api.AdministratorId}/deactivate", null));

            outcomes.Add(new RoundOutcome(responses[0].StatusCode, responses[1].StatusCode, await api.CountActiveAdministratorsAsync()));

            if (!OneWonAndTheOtherWasRefused(outcomes[^1]))
            {
                break;
            }

            (HttpClient survivor, Guid deactivatedId) = responses[0].StatusCode == HttpStatusCode.NoContent
                ? (seed, otherId)
                : (other, api.AdministratorId);
            (await survivor.PutAsync($"users/{deactivatedId}/activate", null)).EnsureSuccessStatusCode();
        }

        // Assert
        outcomes.Count.ShouldBe(Rounds);
        outcomes.ShouldAllBe(outcome => OneWonAndTheOtherWasRefused(outcome));
        outcomes.ShouldAllBe(outcome => outcome.ActiveAdministrators == 1);
    }

    [Fact]
    public async Task AssignRoles_Should_KeepOneActiveAdministrator_WhenRoleRemovalRacesWithDeactivation()
    {
        // Arrange: o administrador do seed tira a função de administrador do outro, que desativa o do seed
        await using IsolatedApi api = await IsolatedApi.StartAsync(_factory, await CreateEmptyDatabaseAsync());
        (Guid otherId, string otherEmail) = await api.CreateAdministratorAsync();
        List<RoundOutcome> outcomes = [];

        // Act: quem ficou como administrador desfaz a mudança do outro para a próxima rodada
        for (int round = 0; round < Rounds; round++)
        {
            using HttpClient seed = await api.LoginAsync(AdminEmail, AdminPassword);
            using HttpClient other = await api.LoginAsync(otherEmail, Password);

            HttpResponseMessage[] responses = await Task.WhenAll(
                seed.PutAsJsonAsync($"users/{otherId}/roles", new { roleNames = new[] { RoleNames.Collaborator } }),
                other.PutAsync($"users/{api.AdministratorId}/deactivate", null));

            outcomes.Add(new RoundOutcome(responses[0].StatusCode, responses[1].StatusCode, await api.CountActiveAdministratorsAsync()));

            if (!OneWonAndTheOtherWasRefused(outcomes[^1]))
            {
                break;
            }

            HttpResponseMessage restore = responses[0].StatusCode == HttpStatusCode.NoContent
                ? await seed.PutAsJsonAsync($"users/{otherId}/roles", new { roleNames = new[] { RoleNames.Administrator } })
                : await other.PutAsync($"users/{api.AdministratorId}/activate", null);
            restore.EnsureSuccessStatusCode();
        }

        // Assert
        outcomes.Count.ShouldBe(Rounds);
        outcomes.ShouldAllBe(outcome => OneWonAndTheOtherWasRefused(outcome));
        outcomes.ShouldAllBe(outcome => outcome.ActiveAdministrators == 1);
    }

    [Fact]
    public async Task AssignRoles_Should_ReturnLastAdministrator_WhenTheOnlyOtherAdministratorIsPendingActivation()
    {
        // Arrange: o outro administrador foi convidado e ainda não definiu a senha
        await using IsolatedApi api = await IsolatedApi.StartAsync(_factory, await CreateEmptyDatabaseAsync());
        using HttpClient seed = await api.LoginAsync(AdminEmail, AdminPassword);
        Guid pendingId = await api.RegisterAsync(seed, UniqueEmail());
        (await seed.PutAsJsonAsync($"users/{pendingId}/roles", new { roleNames = new[] { RoleNames.Administrator } }))
            .EnsureSuccessStatusCode();

        // Act
        HttpResponseMessage response = await seed.PutAsJsonAsync(
            $"users/{api.AdministratorId}/roles",
            new { roleNames = new[] { RoleNames.Collaborator } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        ProblemDto? problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        problem!.Title.ShouldBe("Roles.LastAdministrator");
        (await api.CountActiveAdministratorsAsync()).ShouldBe(1);
    }

    // Uma requisição respondeu 204 e a outra foi recusada: 409 (concorrência ou último administrador) ou 403 (o autor
    // já tinha perdido o acesso quando a requisição dele chegou).
    private static bool OneWonAndTheOtherWasRefused(RoundOutcome outcome) =>
        outcome.First == HttpStatusCode.NoContent && IsRefusal(outcome.Second) ||
        outcome.Second == HttpStatusCode.NoContent && IsRefusal(outcome.First);

    private static bool IsRefusal(HttpStatusCode status) => status is HttpStatusCode.Conflict or HttpStatusCode.Forbidden;

    private sealed record RoundOutcome(HttpStatusCode First, HttpStatusCode Second, int ActiveAdministrators);

    private sealed record ProblemDto(int Status, string Title, string Detail);

    // A API sobre um banco vazio, que ela migra e preenche pelo seed na subida, com o primeiro acesso do administrador do
    // seed já feito, como a fábrica faz no banco compartilhado.
    private sealed class IsolatedApi : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _host;

        private IsolatedApi(WebApplicationFactory<Program> host)
        {
            _host = host;
        }

        public Guid AdministratorId { get; private set; }

        public static async Task<IsolatedApi> StartAsync(IntegrationTestWebAppFactory factory, string connectionString)
        {
            var api = new IsolatedApi(factory.WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Database", connectionString)));

            using HttpClient anonymous = api._host.CreateClient();
            var firstAccess = new
            {
                email = AdminEmail,
                currentPassword = IntegrationTestWebAppFactory.AdminInitialPassword,
                newPassword = AdminPassword
            };
            (await anonymous.PostAsJsonAsync("users/login/new-password", firstAccess)).EnsureSuccessStatusCode();

            using IServiceScope scope = api._host.Services.CreateScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            api.AdministratorId = await context.Users.Where(u => u.Email == AdminEmail).Select(u => u.Id).SingleAsync();

            return api;
        }

        public async Task<HttpClient> LoginAsync(string email, string password)
        {
            using HttpClient anonymous = _host.CreateClient();
            HttpResponseMessage response = await anonymous.PostAsJsonAsync("users/login", new { email, password });
            response.EnsureSuccessStatusCode();
            AccessTokens tokens = (await response.Content.ReadFromJsonAsync<AccessTokens>())!;

            HttpClient client = _host.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

            return client;
        }

        // Cadastra pelo administrador do seed; a conta fica pendente até alguém usar o convite.
        public async Task<Guid> RegisterAsync(HttpClient administrator, string email)
        {
            HttpResponseMessage response = await administrator.PostAsJsonAsync(
                "users/register",
                new { email, firstName = "Test", lastName = "User" });
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<Guid>();
        }

        // Segundo administrador com senha: cadastrado, ativado pelo convite e com a função de administrador.
        public async Task<(Guid Id, string Email)> CreateAdministratorAsync()
        {
            using HttpClient seed = await LoginAsync(AdminEmail, AdminPassword);
            string email = UniqueEmail();
            Guid id = await RegisterAsync(seed, email);

            string token = _host.Services.GetRequiredService<CapturingEmailSender>().LatestTokenFor(email);
            using HttpClient anonymous = _host.CreateClient();
            (await anonymous.PostAsJsonAsync("users/activation", new { token, newPassword = Password })).EnsureSuccessStatusCode();

            (await seed.PutAsJsonAsync($"users/{id}/roles", new { roleNames = new[] { RoleNames.Administrator } }))
                .EnsureSuccessStatusCode();

            return (id, email);
        }

        // Administradores ativos e com senha, como a conferência do último administrador conta.
        public async Task<int> CountActiveAdministratorsAsync()
        {
            using IServiceScope scope = _host.Services.CreateScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            return await context.UserRoles.CountAsync(userRole =>
                userRole.RoleName == RoleNames.Administrator &&
                context.Users.Any(user => user.Id == userRole.UserId && user.IsActive) &&
                context.UserCredentials.Any(credential => credential.UserId == userRole.UserId));
        }

        public ValueTask DisposeAsync() => _host.DisposeAsync();
    }
}
