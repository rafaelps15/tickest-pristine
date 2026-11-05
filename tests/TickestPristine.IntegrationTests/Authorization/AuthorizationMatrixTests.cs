using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Application.Permissions;
using TickestPristine.Infrastructure.Authorization;
using TickestPristine.Web.Api.Endpoints;

namespace TickestPristine.IntegrationTests.Authorization;

public sealed class AuthorizationMatrixTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string DefaultPolicy = "policy padrão";
    private const string AuthenticatedUserPolicy = "policy " + AuthorizationPolicies.AuthenticatedUser;
    private const string Anonymous = "AllowAnonymous";
    private const string NoMetadata = "sem metadado de autorização";
    private const string AnyMethod = "*";
    private const string NonexistentRoute = "rota-inexistente-da-matriz";
    private const string RouteWithGetOnly = "users/me";

    private readonly IntegrationTestWebAppFactory _factory = factory;

    private static readonly Dictionary<string, string> Matrix = new(StringComparer.Ordinal)
    {
        { "GET audit-logs/{entityName}/{entityId}", Permission(PermissionCodes.Audit.Read) },
        { "GET departments", DefaultPolicy },
        { "POST departments", Permission(PermissionCodes.Departments.Manage) },
        { "GET departments/{departmentId:guid}", DefaultPolicy },
        { "PUT departments/{departmentId:guid}", Permission(PermissionCodes.Departments.Manage) },
        { "DELETE departments/{departmentId:guid}", Permission(PermissionCodes.Departments.Manage) },
        { "* health", Anonymous },
        { "* health/live", Anonymous },
        { "* health/ready", Anonymous },
        { "GET permissions", DefaultPolicy },
        { "GET roles", Permission(PermissionCodes.Roles.Read) },
        { "PUT roles/{roleName}/permissions", Permission(PermissionCodes.Roles.Manage) },
        { "GET sectors", DefaultPolicy },
        { "POST sectors", Permission(PermissionCodes.Sectors.Manage) },
        { "GET sectors/{sectorId:guid}", DefaultPolicy },
        { "PUT sectors/{sectorId:guid}", Permission(PermissionCodes.Sectors.Manage) },
        { "DELETE sectors/{sectorId:guid}", Permission(PermissionCodes.Sectors.Manage) },
        { "GET ticket-attachments/policy", DefaultPolicy },
        { "DELETE ticket-attachments/{attachmentId:guid}", DefaultPolicy },
        { "GET ticket-attachments/{attachmentId:guid}/download", DefaultPolicy },
        { "PUT ticket-messages/{messageId:guid}", DefaultPolicy },
        { "DELETE ticket-messages/{messageId:guid}", DefaultPolicy },
        { "GET tickets", Permission(PermissionCodes.Tickets.Manage) },
        { "POST tickets", Permission(PermissionCodes.Tickets.Create) },
        { "GET tickets/assignees", Permission(PermissionCodes.Tickets.Manage) },
        { "GET tickets/mine", Permission(PermissionCodes.Tickets.ViewOwn) },
        { "GET tickets/mine/summary", Permission(PermissionCodes.Tickets.ViewOwn) },
        { "GET tickets/sla-summary", Permission(PermissionCodes.Tickets.Manage) },
        { "GET tickets/stats/overview", Permission(PermissionCodes.Tickets.Manage) },
        { "GET tickets/stats/status", DefaultPolicy },
        { "GET tickets/stats/times", Permission(PermissionCodes.Tickets.Manage) },
        { "GET tickets/stats/volume", DefaultPolicy },
        { "GET tickets/{ticketId:guid}", DefaultPolicy },
        { "PUT tickets/{ticketId:guid}", DefaultPolicy },
        { "DELETE tickets/{ticketId:guid}", DefaultPolicy },
        { "PUT tickets/{ticketId:guid}/assignee", Permission(PermissionCodes.Tickets.Manage) },
        { "DELETE tickets/{ticketId:guid}/assignee", Permission(PermissionCodes.Tickets.Manage) },
        { "GET tickets/{ticketId:guid}/attachments", DefaultPolicy },
        { "POST tickets/{ticketId:guid}/attachments", DefaultPolicy },
        { "POST tickets/{ticketId:guid}/close", Permission(PermissionCodes.Tickets.Manage) },
        { "GET tickets/{ticketId:guid}/history", DefaultPolicy },
        { "GET tickets/{ticketId:guid}/messages", DefaultPolicy },
        { "POST tickets/{ticketId:guid}/messages", DefaultPolicy },
        { "POST tickets/{ticketId:guid}/reopen", DefaultPolicy },
        { "GET tickets/{ticketId:guid}/sla", DefaultPolicy },
        { "GET users", Permission(PermissionCodes.Users.Read) },
        { "POST users/activation", Anonymous },
        { "GET users/attendants", DefaultPolicy },
        { "GET users/by-email", Permission(PermissionCodes.Users.Read) },
        { "POST users/login", Anonymous },
        { "POST users/login/new-password", Anonymous },
        { "POST users/logout", AuthenticatedUserPolicy },
        { "GET users/me", AuthenticatedUserPolicy },
        { "PUT users/me/password", DefaultPolicy },
        { "POST users/refresh-token", Anonymous },
        { "POST users/register", Permission(PermissionCodes.Users.Create) },
        { "GET users/stats/status", Permission(PermissionCodes.Users.Read) },
        { "GET users/{userId:guid}", DefaultPolicy },
        { "PUT users/{userId:guid}/activate", Permission(PermissionCodes.Users.Deactivate) },
        { "PUT users/{userId:guid}/deactivate", Permission(PermissionCodes.Users.Deactivate) },
        { "POST users/{userId:guid}/invitation", Permission(PermissionCodes.Users.Create) },
        { "PUT users/{userId:guid}/profile", DefaultPolicy },
        { "PUT users/{userId:guid}/roles", Permission(PermissionCodes.Users.AssignRoles) },
        { "PUT users/{userId:guid}/sector", Permission(PermissionCodes.Users.Update) },
    };

    [Fact]
    public void Endpoints_Should_MatchAuthorizationMatrix_WhenMapped()
    {
        // Arrange
        List<RouteEndpoint> endpoints = MappedEndpoints();

        // Act
        List<string> divergences = FindDivergences(endpoints, Matrix);

        // Assert
        ShouldHaveNoDivergence(divergences);
    }

    [Fact]
    public void MatrixCheck_Should_FailWithRoute_WhenEndpointHasNoMetadata()
    {
        // Arrange
        List<RouteEndpoint> endpoints = [.. MappedEndpoints(), EndpointWithoutMetadata("GET", "rota-de-teste-sem-protecao")];

        // Act
        List<string> divergences = FindDivergences(endpoints, Matrix);

        // Assert
        ShouldAssertException failure = Should.Throw<ShouldAssertException>(() => ShouldHaveNoDivergence(divergences));
        failure.Message.ShouldContain($"GET rota-de-teste-sem-protecao: rota sem linha na matriz ({NoMetadata})");
    }

    [Fact]
    public void MatrixCheck_Should_FailWithRoute_WhenProtectionDiffersFromMatrix()
    {
        // Arrange
        var matrix = new Dictionary<string, string>(Matrix, StringComparer.Ordinal) { ["GET users"] = Anonymous };

        // Act
        List<string> divergences = FindDivergences(MappedEndpoints(), matrix);

        // Assert
        ShouldAssertException failure = Should.Throw<ShouldAssertException>(() => ShouldHaveNoDivergence(divergences));
        failure.Message.ShouldContain($"GET users: {Permission(PermissionCodes.Users.Read)}, a matriz espera {Anonymous}");
    }

    [Fact]
    public async Task Fallback_Should_ReturnUnauthorized_WhenEndpointHasNoMetadata()
    {
        // Arrange: a API com um endpoint a mais, mapeado sem nenhuma chamada de autorização
        await using WebApplicationFactory<Program> api = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddTransient<IEndpoint, EndpointWithoutAuthorization>()));
        using HttpClient client = api.CreateClient();
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();

        // Act
        HttpResponseMessage withoutToken = await client.GetAsync(EndpointWithoutAuthorization.Route);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        HttpResponseMessage withActiveUserToken = await client.GetAsync(EndpointWithoutAuthorization.Route);

        // Assert
        withoutToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        withActiveUserToken.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHealth_Should_ReturnOk_WhenRequestHasNoToken()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("health");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_Should_Succeed_WhenRequestHasNoToken()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "users/login",
            new { email = AdminEmail, password = AdminPassword });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetNonexistentRoute_Should_ReturnUnauthorized_WhenRequestHasNoToken()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(NonexistentRoute);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetNonexistentRoute_Should_ReturnNotFound_WhenUserIsActive()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(NonexistentRoute);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetNonexistentRoute_Should_ReturnForbidden_WhenUserIsDeactivated()
    {
        // Arrange
        await AuthenticateAsDeactivatedUserAsync();

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(NonexistentRoute);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnsupportedMethod_Should_ReturnUnauthorized_WhenRequestHasNoToken()
    {
        // Arrange
        HttpClient.DefaultRequestHeaders.Authorization = null;

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync(RouteWithGetOnly);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnsupportedMethod_Should_ReturnMethodNotAllowed_WhenUserIsActive()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync(RouteWithGetOnly);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task UnsupportedMethod_Should_ReturnForbidden_WhenUserIsDeactivated()
    {
        // Arrange
        await AuthenticateAsDeactivatedUserAsync();

        // Act
        HttpResponseMessage response = await HttpClient.DeleteAsync(RouteWithGetOnly);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private List<RouteEndpoint> MappedEndpoints()
    {
        return [.. Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];
    }

    private async Task AuthenticateAsDeactivatedUserAsync()
    {
        (Guid userId, AccessTokens userTokens) = await RegisterAndLoginAsync();

        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);
        (await HttpClient.PutAsync($"users/{userId}/deactivate", null)).EnsureSuccessStatusCode();

        Authenticate(userTokens.AccessToken);
    }

    private static string Permission(string code) => $"HasPermission({code})";

    private static List<string> FindDivergences(List<RouteEndpoint> endpoints, Dictionary<string, string> matrix)
    {
        var divergences = new List<string>();
        var mapped = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (RouteEndpoint endpoint in endpoints)
        {
            string protection = DescribeProtection(endpoint);
            IReadOnlyList<string> methods =
                endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods is { Count: > 0 } declared
                    ? declared
                    : [AnyMethod];

            foreach (string method in methods)
            {
                string route = $"{method} {endpoint.RoutePattern.RawText}";

                if (!mapped.TryAdd(route, protection))
                {
                    divergences.Add($"{route}: rota mapeada mais de uma vez");
                }
            }
        }

        foreach ((string route, string protection) in mapped.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!matrix.TryGetValue(route, out string? expected))
            {
                divergences.Add($"{route}: rota sem linha na matriz ({protection})");
            }
            else if (expected != protection)
            {
                divergences.Add($"{route}: {protection}, a matriz espera {expected}");
            }
        }

        divergences.AddRange(matrix.Keys
            .Where(route => !mapped.ContainsKey(route))
            .Order(StringComparer.Ordinal)
            .Select(route => $"{route}: linha da matriz sem rota mapeada"));

        return divergences;
    }

    private static string DescribeProtection(RouteEndpoint endpoint)
    {
        var parts = new List<string>();

        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            parts.Add(Anonymous);
        }

        foreach (IAuthorizeData data in endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>())
        {
            parts.Add(data switch
            {
                HasPermissionAttribute { Policy: { } code } => Permission(code),
                { Policy: null, Roles: null, AuthenticationSchemes: null } => DefaultPolicy,
                { Policy: { } policy, Roles: null, AuthenticationSchemes: null } => $"policy {policy}",
                _ => $"autorização com policy '{data.Policy}', funções '{data.Roles}' e esquemas '{data.AuthenticationSchemes}'",
            });
        }

        parts.AddRange(endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>().Select(_ => "policy montada na rota"));

        return parts.Count == 0 ? NoMetadata : string.Join(" + ", parts);
    }

    private static void ShouldHaveNoDivergence(List<string> divergences)
    {
        divergences.ShouldBeEmpty(
            "Endpoints fora da matriz de autorização:" + Environment.NewLine + string.Join(Environment.NewLine, divergences));
    }

    private static RouteEndpoint EndpointWithoutMetadata(string method, string pattern)
    {
        var builder = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse(pattern), order: 0);
        builder.Metadata.Add(new HttpMethodMetadata([method]));

        return (RouteEndpoint)builder.Build();
    }
}

// Endpoint de teste mapeado sem nenhuma chamada de autorização, para conferir a FallbackPolicy na API real.
internal sealed class EndpointWithoutAuthorization : IEndpoint
{
    internal const string Route = "teste/sem-metadado-de-autorizacao";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(Route, () => Results.Ok());
    }
}
