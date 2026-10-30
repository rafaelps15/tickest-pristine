using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json;
using TickestPristine.Application.Users;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public abstract class BaseIntegrationTest
{
    private string? _adminAccessToken;

    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        HttpClient = factory.CreateClient();
        Services = factory.Services;
    }

    protected const string AdminEmail = IntegrationTestWebAppFactory.AdminEmail;
    protected const string AdminPassword = IntegrationTestWebAppFactory.AdminPassword;

    protected HttpClient HttpClient { get; }

    protected IServiceProvider Services { get; }

    protected sealed record AccessTokens(string AccessToken, string RefreshToken);

    /// <summary>
    /// Página de uma listagem por cursor composto (data + id), como devolvida pela API.
    /// </summary>
    protected sealed record CursorPageDto<T>(List<T> Items, DateTime? NextCursorDate, Guid? NextCursorId, bool HasNextPage);

    protected static string UniqueEmail() => $"test-{Guid.NewGuid():N}@example.com";

    /// <summary>
    /// Mensagens enviadas pela API nos testes (o envio real é trocado no <see cref="IntegrationTestWebAppFactory"/>).
    /// </summary>
    protected CapturingEmailSender EmailSender => Services.GetRequiredService<CapturingEmailSender>();

    /// <summary>
    /// Cadastra o usuário pelo administrador e ativa a conta com o token do e-mail de convite, definindo a senha
    /// <c>Password123!</c>: depois disso, o usuário já faz login, como antes do cadastro sem senha (spec 008, DEC-12).
    /// </summary>
    protected async Task<Guid> RegisterUserAsync(string email)
    {
        var request = new
        {
            email,
            firstName = "Test",
            lastName = "User",
            password = "Password123!"
        };

        HttpResponseMessage response = await PostRegisterAsAdminAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>
    /// Envia o cadastro como o administrador, que tem <c>users:create</c>. O token vai só nesta requisição, sem trocar
    /// o token que o teste configurou no <see cref="HttpClient"/>. O cadastro não recebe mais senha (spec 008): quando o
    /// pedido traz <c>password</c>, o auxiliar ativa a conta com ela, pelo token do e-mail de convite (DEC-12).
    /// </summary>
    protected async Task<HttpResponseMessage> PostRegisterAsAdminAsync(object request)
    {
        _adminAccessToken ??= (await LoginAsync(AdminEmail, AdminPassword)).AccessToken;

        using var message = new HttpRequestMessage(HttpMethod.Post, "users/register")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _adminAccessToken);

        HttpResponseMessage response = await HttpClient.SendAsync(message);

        JsonElement body = JsonSerializer.SerializeToElement(request);

        if (response.IsSuccessStatusCode &&
            body.TryGetProperty("email", out JsonElement email) &&
            body.TryGetProperty("password", out JsonElement password))
        {
            (await ActivateAccountAsync(EmailSender.LatestTokenFor(email.GetString()!), password.GetString()!))
                .EnsureSuccessStatusCode();
        }

        return response;
    }

    /// <summary>
    /// Ativa a conta pelo token do convite (rota anônima), sem trocar o token configurado no <see cref="HttpClient"/>.
    /// </summary>
    protected async Task<HttpResponseMessage> ActivateAccountAsync(string token, string newPassword)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "users/activation")
        {
            Content = JsonContent.Create(new { token, newPassword })
        };

        return await HttpClient.SendAsync(message);
    }

    protected Task<AccessTokens> LoginAsync(string email) => LoginAsync(email, "Password123!");

    protected async Task<AccessTokens> LoginAsync(string email, string password)
    {
        var request = new { email, password };

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("users/login", request);
        response.EnsureSuccessStatusCode();

        AccessTokens? tokens = await response.Content.ReadFromJsonAsync<AccessTokens>();

        return tokens!;
    }

    protected async Task<(Guid UserId, AccessTokens Tokens)> RegisterAndLoginAsync()
    {
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);
        AccessTokens tokens = await LoginAsync(email);

        return (userId, tokens);
    }

    protected void Authenticate(string accessToken)
    {
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    /// <summary>
    /// Id do usuário dono do token (claim "sub").
    /// </summary>
    protected static Guid GetUserId(AccessTokens tokens) =>
        Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken).Subject);

    /// <summary>
    /// Cria uma role isolada para o teste: as roles fixas são compartilhadas por todos os testes e não devem ser
    /// alteradas. A API não cria roles, então ela entra direto pelo contexto.
    /// </summary>
    protected async Task<string> CreateTestRoleAsync()
    {
        string name = $"Role-{Guid.NewGuid():N}";

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Roles.Add(new Role { Id = Guid.NewGuid(), Name = name });
        await context.SaveChangesAsync();

        return name;
    }

    /// <summary>
    /// Usuário gravado direto no banco, com as roles informadas e sem senha (não faz login): para os testes que só
    /// precisam dele em consultas e atribuições.
    /// </summary>
    protected async Task<Guid> InsertUserAsync(
        string firstName,
        string lastName,
        string email,
        Guid? sectorId = null,
        bool isActive = true,
        params string[] roleNames)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Code = UserCodes.New(),
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow,
            DeactivatedAtUtc = isActive ? null : DateTime.UtcNow,
            SectorId = sectorId
        };

        context.Users.Add(user);
        context.UserRoles.AddRange(roleNames.Select(roleName => new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RoleName = roleName
        }));
        await context.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>
    /// Setor ativo num departamento novo, gravados direto no banco. O nome do departamento precisa ser único entre os
    /// ativos.
    /// </summary>
    protected async Task<Guid> InsertSectorAsync(string sectorName, string departmentName)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var department = Department.Create(departmentName, "A department created by tests", responsibleUser: null);
        var sector = Sector.Create(department, sectorName, description: null);

        context.Departments.Add(department);
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        return sector.Id;
    }
}
