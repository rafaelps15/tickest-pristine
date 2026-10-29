using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Application.TicketHistories;
using TickestPristine.Application.Tickets;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Tickets;

/// <summary>
/// Última atividade do chamado em "Meus chamados" (spec 015): o interceptor que a mantém a cada registro de histórico e
/// a migration que preenche os chamados existentes.
/// </summary>
public sealed class TicketActivityTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string LastActivityMigration = "Add_Ticket_Last_Activity";

    [Fact]
    public async Task LastActivity_Should_EqualCreatedAt_WhenTicketIsNew()
    {
        // Arrange
        (Guid ticketId, _) = await CreateTicketAsRequesterAsync();

        // Act
        OwnTicketDto ticket = await GetOwnTicketAsync(ticketId);

        // Assert
        ticket.LastActivityAtUtc.ShouldBe(ticket.CreatedAtUtc);
    }

    [Fact]
    public async Task LastActivity_Should_FollowLatestHistoryEntry_WhenAgentPostsMessage()
    {
        // Arrange: o chamado de Maria, aberto agora
        (Guid ticketId, AccessTokens requesterTokens) = await CreateTicketAsRequesterAsync();
        DateTime createdAtUtc = (await GetOwnTicketAsync(ticketId)).CreatedAtUtc;

        // Act: quem atende publica uma mensagem; Maria consulta "Meus chamados"
        await AuthenticateAsAdminAsync();
        (await HttpClient.PostAsJsonAsync($"tickets/{ticketId}/messages", new { content = "Estamos verificando." }))
            .EnsureSuccessStatusCode();
        Authenticate(requesterTokens.AccessToken);
        OwnTicketDto ticket = await GetOwnTicketAsync(ticketId);

        // Assert
        DateTime messageAtUtc = await GetLatestHistoryAtAsync(ticketId, TicketHistoryAction.MessageAdded);
        ticket.LastActivityAtUtc.ShouldBe(messageAtUtc);
        ticket.LastActivityAtUtc.ShouldBeGreaterThan(createdAtUtc);
    }

    [Fact]
    public async Task LastActivity_Should_AdvanceWithEachAction_WhenStatusChangesAndFileIsAttached()
    {
        // Arrange: o chamado de Maria, com uma mensagem de quem atende
        (Guid ticketId, AccessTokens requesterTokens) = await CreateTicketAsRequesterAsync();
        await AuthenticateAsAdminAsync();
        (await HttpClient.PostAsJsonAsync($"tickets/{ticketId}/messages", new { content = "Estamos verificando." }))
            .EnsureSuccessStatusCode();
        DateTime afterMessage = await GetLastActivityAsAsync(requesterTokens, ticketId);

        // Act: troca de status e, depois, um anexo
        await AuthenticateAsAdminAsync();
        (await HttpClient.PutAsJsonAsync($"tickets/{ticketId}", new
        {
            description = "The printer on the 3rd floor is not working",
            status = 2 // InProgress
        })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        DateTime afterStatusChange = await GetLastActivityAsAsync(requesterTokens, ticketId);

        await AuthenticateAsAdminAsync();
        (await UploadAttachmentAsync(ticketId)).EnsureSuccessStatusCode();
        DateTime afterAttachment = await GetLastActivityAsAsync(requesterTokens, ticketId);

        // Assert
        afterStatusChange.ShouldBe(await GetLatestHistoryAtAsync(ticketId, TicketHistoryAction.StatusChanged));
        afterStatusChange.ShouldBeGreaterThan(afterMessage);
        afterAttachment.ShouldBe(await GetLatestHistoryAtAsync(ticketId, TicketHistoryAction.AttachmentAdded));
        afterAttachment.ShouldBeGreaterThan(afterStatusChange);
    }

    [Fact]
    public async Task LastActivity_Should_NeverMoveBackwards_WhenOlderHistoryEntryIsSaved()
    {
        // Arrange
        (Guid ticketId, AccessTokens requesterTokens) = await CreateTicketAsRequesterAsync();
        DateTime before = await GetLastActivityAsAsync(requesterTokens, ticketId);

        // Act: um registro de histórico com data anterior à atividade atual
        using (IServiceScope scope = Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.TicketHistories.Add(TicketHistoryEntries.Create(
                ticketId,
                GetUserId(requesterTokens),
                TicketHistoryAction.MessageAdded,
                before.AddHours(-1)));
            await context.SaveChangesAsync();
        }

        DateTime after = await GetLastActivityAsAsync(requesterTokens, ticketId);

        // Assert
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Migration_Should_BackfillLastActivityFromHistory_WhenApplied()
    {
        // Arrange: um chamado com histórico e outro sem nenhum registro de histórico, os dois com a coluna errada
        (Guid requesterId, Guid sectorId) = await CreateRequesterAndSectorAsync();
        DateTime createdAtUtc = DateTime.UtcNow.AddDays(-3);
        DateTime historyAtUtc = createdAtUtc.AddDays(1);
        Guid withHistoryId = await InsertTicketAsync(requesterId, sectorId, createdAtUtc);
        Guid withoutHistoryId = await InsertTicketAsync(requesterId, sectorId, createdAtUtc);

        await ExecuteAsync(async context =>
        {
            await context.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO public.ticket_histories (id, ticket_id, changed_by_user_id, action, occurred_at_utc)
                VALUES ({Guid.NewGuid()}, {withHistoryId}, {requesterId}, 'MessageAdded', {historyAtUtc}),
                       ({Guid.NewGuid()}, {withHistoryId}, {requesterId}, 'Created', {createdAtUtc})
                """);
            await context.Database.ExecuteSqlAsync(
                $"UPDATE public.tickets SET last_activity_at_utc = {DateTime.MinValue} WHERE id IN ({withHistoryId}, {withoutHistoryId})");
        });

        // Act: desfaz a migration e aplica de novo
        await ExecuteAsync(async context =>
        {
            IMigrator migrator = context.GetService<IMigrator>();
            var migrations = context.Database.GetMigrations().ToList();
            int index = migrations.FindIndex(m => m.EndsWith(LastActivityMigration, StringComparison.Ordinal));

            try
            {
                await migrator.MigrateAsync(migrations[index - 1]);
                await migrator.MigrateAsync(migrations[index]);
            }
            finally
            {
                // O banco é compartilhado pela coleção: volta à última migration, mesmo se o teste falhar no meio. Sem isso,
                // os testes seguintes ficariam sem as colunas das migrations posteriores (spec 029).
                await migrator.MigrateAsync();
            }
        });

        // Assert
        await ExecuteAsync(async context =>
        {
            Dictionary<Guid, DateTime> lastActivity = await context.Tickets.IgnoreQueryFilters()
                .Where(t => t.Id == withHistoryId || t.Id == withoutHistoryId)
                .ToDictionaryAsync(t => t.Id, t => t.LastActivityAtUtc);

            lastActivity[withHistoryId].ShouldBe(historyAtUtc, TimeSpan.FromMilliseconds(1));
            lastActivity[withoutHistoryId].ShouldBe(createdAtUtc, TimeSpan.FromMilliseconds(1));
        });
    }

    private async Task AuthenticateAsAdminAsync()
    {
        AccessTokens tokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(tokens.AccessToken);
    }

    private async Task<Guid> CreateSectorAsAdminAsync()
    {
        await AuthenticateAsAdminAsync();

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

    private async Task<(Guid RequesterId, Guid SectorId)> CreateRequesterAndSectorAsync()
    {
        Guid sectorId = await CreateSectorAsAdminAsync();
        (Guid requesterId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        return (requesterId, sectorId);
    }

    /// <summary>
    /// Chamado aberto pela API por um Colaborador novo, que fica autenticado.
    /// </summary>
    private async Task<(Guid TicketId, AccessTokens RequesterTokens)> CreateTicketAsRequesterAsync()
    {
        Guid sectorId = await CreateSectorAsAdminAsync();
        (_, AccessTokens requesterTokens) = await RegisterAndLoginAsync();
        Authenticate(requesterTokens.AccessToken);

        HttpResponseMessage response = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = "Printer is broken",
            description = "The printer on the 3rd floor is not working",
            priority = 2,
            sectorId
        });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<Guid>(), requesterTokens);
    }

    private async Task<OwnTicketDto> GetOwnTicketAsync(Guid ticketId)
    {
        PagedOwnTicketsDto? page = await HttpClient.GetFromJsonAsync<PagedOwnTicketsDto>("tickets/mine?pageSize=100");

        return page!.Items.Single(t => t.Id == ticketId);
    }

    private async Task<DateTime> GetLastActivityAsAsync(AccessTokens tokens, Guid ticketId)
    {
        Authenticate(tokens.AccessToken);

        return (await GetOwnTicketAsync(ticketId)).LastActivityAtUtc;
    }

    private async Task<HttpResponseMessage> UploadAttachmentAsync(Guid ticketId)
    {
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.7 test file content"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        using var content = new MultipartFormDataContent { { fileContent, "file", "report.pdf" } };

        return await HttpClient.PostAsync($"tickets/{ticketId}/attachments", content);
    }

    private async Task<DateTime> GetLatestHistoryAtAsync(Guid ticketId, TicketHistoryAction action)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await context.TicketHistories
            .Where(h => h.TicketId == ticketId && h.Action == action)
            .MaxAsync(h => h.OccurredAtUtc);
    }

    /// <summary>
    /// Chamado gravado direto no banco, sem registro de histórico.
    /// </summary>
    private async Task<Guid> InsertTicketAsync(Guid requesterId, Guid sectorId, DateTime createdAtUtc)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = "Printer is broken",
            Description = "The printer on the 3rd floor is not working",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Open,
            CreatedByUserId = requesterId,
            SectorId = sectorId,
            CreatedAtUtc = createdAtUtc,
            LastActivityAtUtc = createdAtUtc,
            DueAtUtc = createdAtUtc.AddHours(8),
            SlaWarningAtUtc = createdAtUtc.AddHours(6),
            SlaPlan = TicketSla.LegacyPlan(TicketPriority.Medium)
        };

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }

    private async Task ExecuteAsync(Func<ApplicationDbContext, Task> action)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await action(context);
    }

    private sealed record OwnTicketDto(Guid Id, DateTime CreatedAtUtc, DateTime LastActivityAtUtc);

    private sealed record PagedOwnTicketsDto(List<OwnTicketDto> Items, int TotalCount);
}
