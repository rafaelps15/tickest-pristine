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
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.TicketHistories;

namespace TickestPristine.IntegrationTests.Tickets;

// Última atividade do chamado em "Meus chamados": o interceptor que a mantém a cada registro de histórico e
// a migration que preenche os chamados existentes.
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
            Ticket ticket = await context.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId);
            context.TicketHistories.Add(ticket
                .PostMessage(GetUserId(requesterTokens), "Mensagem antiga", before.AddHours(-1))
                .History);
            await context.SaveChangesAsync();
        }

        DateTime after = await GetLastActivityAsAsync(requesterTokens, ticketId);

        // Assert
        after.ShouldBe(before);
    }

    [Fact]
    public async Task WaitingOn_Should_FollowLastValidMessage_When_EarlierMessageWritesLast()
    {
        // Arrange: o chamado de Maria; quem atende responde, e Maria responde enquanto a projeção da resposta de quem
        // atende ainda não gravou.
        (Guid ticketId, AccessTokens requesterTokens) = await CreateTicketAsRequesterAsync();
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);

        // Gatilho temporário que segura por dois segundos o primeiro UPDATE de chamados, antes de ele travar a linha: sem a
        // trava da projeção, a resposta de Maria gravaria antes, e a de quem atende, calculada sem ela, por cima.
        await ExecuteAsync(context => context.Database.ExecuteSqlRawAsync(HoldFirstTicketUpdateSql));

        HttpResponseMessage[] responses;

        try
        {
            // Act
            Task<HttpResponseMessage> agentAnswer = SendMessageAsAsync(adminTokens, ticketId, "Estamos verificando.");
            await WaitForMessageAsync(ticketId);
            Task<HttpResponseMessage> requesterAnswer = SendMessageAsAsync(requesterTokens, ticketId, "Ainda não funciona.");
            responses = await Task.WhenAll(agentAnswer, requesterAnswer);
        }
        finally
        {
            await ExecuteAsync(context => context.Database.ExecuteSqlRawAsync(DropHoldFirstTicketUpdateSql));
        }

        // Assert: a última mensagem válida é de Maria, então o chamado aguarda quem atende.
        responses.ShouldAllBe(response => response.IsSuccessStatusCode);
        DateTime lastMessageAtUtc = await GetLatestHistoryAtAsync(ticketId, TicketHistoryAction.MessageAdded);

        await ExecuteAsync(async context =>
        {
            Ticket stored = await context.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId);
            stored.WaitingOn.ShouldBe(TicketWaitingOn.Agent);
            stored.LastActivityAtUtc.ShouldBe(lastMessageAtUtc);
        });
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
                // os testes seguintes ficariam sem as colunas das migrations posteriores.
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

    // Chamado aberto pela API por um Colaborador novo, que fica autenticado.
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

    // Chamado gravado direto no banco, sem registro de histórico.
    private async Task<Guid> InsertTicketAsync(Guid requesterId, Guid sectorId, DateTime createdAtUtc)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Sector sector = await context.Sectors.SingleAsync(s => s.Id == sectorId);

        Ticket ticket = TicketTestData.Create(
            sector,
            requesterId: requesterId,
            createdAtUtc: createdAtUtc,
            slaPlan: TicketSlaPlan.Legacy(TicketPriority.Medium),
            dueAtUtc: createdAtUtc.AddHours(8),
            slaWarningAtUtc: createdAtUtc.AddHours(6));

        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        return ticket.Id;
    }

    private async Task<HttpResponseMessage> SendMessageAsAsync(AccessTokens tokens, Guid ticketId, string content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"tickets/{ticketId}/messages")
        {
            Content = JsonContent.Create(new { content })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return await HttpClient.SendAsync(request);
    }

    // A mensagem aparece no banco no commit, antes da projeção dela.
    private async Task WaitForMessageAsync(Guid ticketId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        bool posted = false;

        while (!posted)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
            await ExecuteAsync(async context => posted = await context.TicketMessages.AnyAsync(m => m.TicketId == ticketId));
        }
    }

    // Só o primeiro UPDATE de chamados espera, antes de travar a linha; a sequência não bloqueia os seguintes.
    private const string HoldFirstTicketUpdateSql =
        """
        CREATE SEQUENCE public.test_hold_first_ticket_update;

        CREATE OR REPLACE FUNCTION public.test_hold_first_ticket_update() RETURNS trigger AS $$
        BEGIN
            IF nextval('public.test_hold_first_ticket_update') = 1 THEN
                PERFORM pg_sleep(2);
            END IF;
            RETURN NULL;
        END;
        $$ LANGUAGE plpgsql;

        CREATE TRIGGER test_hold_first_ticket_update
            BEFORE UPDATE ON public.tickets
            FOR EACH STATEMENT
            EXECUTE FUNCTION public.test_hold_first_ticket_update();
        """;

    private const string DropHoldFirstTicketUpdateSql =
        """
        DROP TRIGGER IF EXISTS test_hold_first_ticket_update ON public.tickets;
        DROP FUNCTION IF EXISTS public.test_hold_first_ticket_update();
        DROP SEQUENCE IF EXISTS public.test_hold_first_ticket_update;
        """;

    private async Task ExecuteAsync(Func<ApplicationDbContext, Task> action)
    {
        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await action(context);
    }

    private sealed record OwnTicketDto(Guid Id, DateTime CreatedAtUtc, DateTime LastActivityAtUtc);

    private sealed record PagedOwnTicketsDto(List<OwnTicketDto> Items, int TotalCount);
}
