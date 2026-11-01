using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;

namespace TickestPristine.IntegrationTests.Database;

// Valida que o token de concorrência do chamado (xmin) impede que uma mudança de status sobrescreva outra.
public sealed class TicketConcurrencyTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task SaveChanges_Should_RejectSecondStatusChange_WhenBothReadTheSameVersion()
    {
        // Arrange
        // Dois contextos leem o mesmo chamado antes de qualquer um gravar.
        Guid ticketId = await CreateTicketAsync();

        using IServiceScope firstScope = Services.CreateScope();
        using IServiceScope secondScope = Services.CreateScope();
        ApplicationDbContext firstContext = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationDbContext secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Ticket firstCopy = await firstContext.Tickets.SingleAsync(t => t.Id == ticketId);
        Ticket secondCopy = await secondContext.Tickets.SingleAsync(t => t.Id == ticketId);

        firstCopy.Update(firstCopy.Description, TicketStatus.InProgress, [], lastMessageIsFromAgent: false, firstCopy.CreatedByUserId, DateTime.UtcNow);
        secondCopy.Update(secondCopy.Description, TicketStatus.Canceled, [], lastMessageIsFromAgent: false, secondCopy.CreatedByUserId, DateTime.UtcNow);
        firstCopy.ClearDomainEvents();
        secondCopy.ClearDomainEvents();

        // Act
        await firstContext.SaveChangesAsync();
        Func<Task> secondSave = () => secondContext.SaveChangesAsync();

        // Assert
        await secondSave.ShouldThrowAsync<DbUpdateConcurrencyException>();

        using IServiceScope readScope = Services.CreateScope();
        ApplicationDbContext readContext = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Ticket stored = await readContext.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId);
        stored.Status.ShouldBe(TicketStatus.InProgress);
    }

    private async Task<Guid> CreateTicketAsync()
    {
        AccessTokens adminTokens = await LoginAsync(AdminEmail, AdminPassword);
        Authenticate(adminTokens.AccessToken);

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
        Guid sectorId = await sectorResponse.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage ticketResponse = await HttpClient.PostAsJsonAsync("tickets", new
        {
            title = "Printer is broken",
            description = "The printer on the 3rd floor is not working",
            priority = 2,
            sectorId
        });
        ticketResponse.EnsureSuccessStatusCode();

        return await ticketResponse.Content.ReadFromJsonAsync<Guid>();
    }
}
