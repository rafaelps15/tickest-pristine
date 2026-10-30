using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Database;

public sealed class TextLengthLimitsTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Theory]
    [InlineData("UPDATE public.tickets SET title = repeat('a', 201) WHERE id = (SELECT id FROM public.tickets LIMIT 1)", "ck_tickets_title_length", true)]
    [InlineData("UPDATE public.tickets SET description = repeat('a', 501) WHERE id = (SELECT id FROM public.tickets LIMIT 1)", "ck_tickets_description_length", true)]
    [InlineData("UPDATE public.tickets SET description = repeat('a', 9) WHERE id = (SELECT id FROM public.tickets LIMIT 1)", "ck_tickets_description_length", true)]
    [InlineData("UPDATE public.users SET first_name = repeat('a', 101) WHERE id = (SELECT id FROM public.users LIMIT 1)", "ck_users_first_name_length", false)]
    [InlineData("UPDATE public.users SET last_name = repeat('a', 101) WHERE id = (SELECT id FROM public.users LIMIT 1)", "ck_users_last_name_length", false)]
    [InlineData("UPDATE public.users SET email = repeat('a', 255) WHERE id = (SELECT id FROM public.users LIMIT 1)", "ck_users_email_length", false)]
    public async Task Database_Should_RejectCitextOutsideLimits_WhenWrittenDirectly(string sql, string constraint, bool needsTicket)
    {
        // Arrange: uma linha existente na tabela (o administrador ou um chamado criado pela API)
        if (needsTicket)
        {
            await CreateTicketAsync(await CreateSectorAsAdminAsync());
        }

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        PostgresException exception = await Should.ThrowAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(sql));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe(constraint);
    }

    [Fact]
    public async Task CreateTicket_Should_ReturnBadRequest_WhenTitleIsAboveColumnLimit()
    {
        // Arrange
        Guid sectorId = await CreateSectorAsAdminAsync();

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = new string('a', 201),
            description = "The printer on the 3rd floor is not working",
            priority = 2,
            sectorId
        });

        // Assert
        await ShouldBeTextTooLongAsync(response);
    }

    [Fact]
    public async Task PostAndEditMessage_Should_ReturnBadRequest_WhenContentIsAboveColumnLimit()
    {
        // Arrange
        Guid ticketId = await CreateTicketAsync(await CreateSectorAsAdminAsync());
        HttpResponseMessage posted = await HttpClient.PostAsJsonAsync($"tickets/{ticketId}/messages", new { content = "Olá, já estou vendo." });
        posted.EnsureSuccessStatusCode();
        Guid messageId = await posted.Content.ReadFromJsonAsync<Guid>();

        // Act
        HttpResponseMessage post = await HttpClient.PostAsJsonAsync(
            $"tickets/{ticketId}/messages",
            new { content = new string('a', 4001) });
        HttpResponseMessage edit = await HttpClient.PutAsJsonAsync(
            $"ticket-messages/{messageId}",
            new { content = new string('a', 4001) });

        // Assert
        await ShouldBeTextTooLongAsync(post);
        await ShouldBeTextTooLongAsync(edit);
    }

    [Fact]
    public async Task RegisterAndUpdateProfile_Should_ReturnBadRequest_WhenNameIsAboveColumnLimit()
    {
        // Arrange
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

        // Act
        HttpResponseMessage register = await PostRegisterAsAdminAsync(new
        {
            email = UniqueEmail(),
            firstName = new string('a', 101),
            lastName = "User"
        });
        HttpResponseMessage profile = await HttpClient.PutAsJsonAsync(
            $"users/{GetUserId(adminTokens)}/profile",
            new { firstName = "Admin", lastName = new string('a', 101) });

        // Assert
        await ShouldBeTextTooLongAsync(register);
        await ShouldBeTextTooLongAsync(profile);
    }

    private static async Task ShouldBeTextTooLongAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        ProblemDetails problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
        problem.Title.ShouldBe("Texto fora do tamanho permitido");
        problem.Detail.ShouldBe("Um dos textos enviados está fora do tamanho permitido. Confira o tamanho e tente novamente.");
    }

    private async Task<Guid> CreateSectorAsAdminAsync()
    {
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);

        HttpResponseMessage departmentResponse = await HttpClient.PostAsJsonAsync("departments", new
        {
            name = $"Department-{Guid.NewGuid():N}",
            description = "A department created by tests"
        });
        departmentResponse.EnsureSuccessStatusCode();
        Guid departmentId = await departmentResponse.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage sectorResponse = await HttpClient.PostAsJsonAsync("sectors", new
        {
            name = $"Sector-{Guid.NewGuid():N}",
            description = "A sector created by tests",
            departmentId
        });
        sectorResponse.EnsureSuccessStatusCode();

        return await sectorResponse.Content.ReadFromJsonAsync<Guid>();
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
}
