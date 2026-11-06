using System.Data.Common;
using Dapper;
using TickestPristine.Domain.Tickets;
using TickestPristine.Infrastructure.Database;
using TickestPristine.IntegrationTests.Abstractions;

namespace TickestPristine.IntegrationTests.Database;

public sealed class DapperTypeMappingTests(IntegrationTestWebAppFactory factory) : BaseQueryHandlerTest(factory)
{
    private const string Sql =
        $"""
         SELECT
             t.status AS {nameof(TicketRow.Status)},
             t.priority AS {nameof(TicketRow.Priority)},
             t.waiting_on AS {nameof(TicketRow.WaitingOn)},
             t.sla_plan AS {nameof(TicketRow.SlaPlan)},
             t.created_at_utc AS {nameof(TicketRow.CreatedAtUtc)},
             (t.created_at_utc AT TIME ZONE 'America/Sao_Paulo')::date AS {nameof(TicketRow.CreatedOn)}
         FROM tickets t
         WHERE t.id = @TicketId
         """;

    private static readonly DateTime OpenedAtUtc = new(2026, 10, 26, 2, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Query_Should_ReadEnumsPlanAndDatesAsTheEntityHasThem_WhenTicketIsStored()
    {
        // Arrange
        var plan = TicketSlaPlan.Default(TicketPriority.Urgent);
        Ticket ticket = QueryTicketTestData.Create(
            status: TicketStatus.InProgress,
            createdAtUtc: OpenedAtUtc,
            priority: TicketPriority.Urgent,
            slaPlan: plan);
        ticket.With(nameof(Ticket.WaitingOn), TicketWaitingOn.Agent);
        await InsertAsync(ticket);

        // Act
        TicketRow row = await ReadAsync(ticket.Id);

        // Assert
        row.Status.ShouldBe(TicketStatus.InProgress);
        row.Priority.ShouldBe(TicketPriority.Urgent);
        row.WaitingOn.ShouldBe(TicketWaitingOn.Agent);
        row.SlaPlan.ShouldBe(plan);
        row.CreatedAtUtc.ShouldBe(OpenedAtUtc);
        row.CreatedAtUtc.Kind.ShouldBe(DateTimeKind.Utc);
        row.CreatedOn.ShouldBe(new DateOnly(2026, 10, 25));
    }

    [Fact]
    public async Task Query_Should_ReadNullEnum_WhenColumnIsNull()
    {
        // Arrange
        Ticket ticket = QueryTicketTestData.Create(createdAtUtc: OpenedAtUtc);
        await InsertAsync(ticket);

        // Act
        TicketRow row = await ReadAsync(ticket.Id);

        // Assert
        row.WaitingOn.ShouldBeNull();
        row.SlaPlan.ShouldBe(TicketSlaPlan.Legacy(TicketPriority.Medium));
    }

    private async Task InsertAsync(Ticket ticket)
    {
        await using ApplicationDbContext context = CreateDbContext();
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();
    }

    private async Task<TicketRow> ReadAsync(Guid ticketId)
    {
        await using DbConnection connection = await DbConnectionFactory.OpenConnectionAsync();

        return await connection.QuerySingleAsync<TicketRow>(Sql, new { TicketId = ticketId });
    }

    public sealed class TicketRow
    {
        public TicketStatus Status { get; init; }
        public TicketPriority Priority { get; init; }
        public TicketWaitingOn? WaitingOn { get; init; }
        public TicketSlaPlan SlaPlan { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateOnly CreatedOn { get; init; }
    }
}
