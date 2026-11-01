using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.IntegrationTests;

// Monta chamados pelos métodos de Ticket: abre com o Create e leva ao status pedido pelo caminho
// que a API permite (Em andamento, Resolvido, Fechado ou Cancelado), com a exclusão no fim, se pedida. As entradas do
// histórico e os eventos do arranjo são descartados. O setor e os usuários informados precisam existir no banco, por
// causa das chaves estrangeiras. Sem plano informado, o chamado recebe o legado; os prazos e a última atividade ficam
// como o teste os pede (prazos zerados e a abertura, quando não informados), para o cenário não depender do calendário.
internal static class TicketTestData
{
    public static Ticket Create(
        Sector sector,
        Guid? requesterId = null,
        TicketStatus status = TicketStatus.Open,
        DateTime? createdAtUtc = null,
        Guid? assignedToUserId = null,
        TicketPriority priority = TicketPriority.Medium,
        string title = "Printer is broken",
        string description = "The printer on the 3rd floor is not working",
        TicketSlaPlan? slaPlan = null,
        DateTime dueAtUtc = default,
        DateTime slaWarningAtUtc = default,
        DateTime? resolvedAtUtc = null,
        DateTime? deletedAtUtc = null)
    {
        DateTime openedAtUtc = createdAtUtc ?? DateTime.UtcNow;
        var requester = new User { Id = requesterId ?? Guid.NewGuid() };
        User? assignee = assignedToUserId is { } assigneeId ? new User { Id = assigneeId } : null;

        var ticket = Ticket.Create(
            title,
            description,
            priority,
            sector,
            requester,
            assignee,
            slaPlan ?? TicketSlaPlan.Legacy(priority),
            ContinuousSlaCalendar.Create(),
            requester.Id,
            openedAtUtc);

        if (status is TicketStatus.InProgress or TicketStatus.Resolved or TicketStatus.Closed)
        {
            ticket.Update(description, TicketStatus.InProgress, [], lastMessageIsFromAgent: false, requester.Id, openedAtUtc);
        }

        if (status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            ticket.Update(description, TicketStatus.Resolved, [], lastMessageIsFromAgent: false, requester.Id, resolvedAtUtc ?? openedAtUtc);
        }

        if (status == TicketStatus.Closed)
        {
            ticket.Close(requester.Id, resolvedAtUtc ?? openedAtUtc);
        }

        if (status == TicketStatus.Canceled)
        {
            ticket.Update(description, TicketStatus.Canceled, [], lastMessageIsFromAgent: false, requester.Id, openedAtUtc);
        }

        if (deletedAtUtc is { } deletedAt)
        {
            ticket.Delete([], requester.Id, deletedAt);
        }

        ticket
            .With(nameof(Ticket.DueAtUtc), dueAtUtc)
            .With(nameof(Ticket.SlaWarningAtUtc), slaWarningAtUtc)
            .With(nameof(Ticket.LastActivityAtUtc), openedAtUtc);

        ticket.ClearDomainEvents();

        return ticket;
    }
}
