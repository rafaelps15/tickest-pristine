using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Abstractions;

// Monta chamados pelos métodos de Ticket: abre com o Create e leva ao status pedido pelo caminho
// que a API permite (Em andamento, Resolvido, Fechado ou Cancelado), com a exclusão no fim, se pedida. As entradas do
// histórico e os eventos do arranjo são descartados, para o teste ver só o que o handler produz.
internal static class TicketTestData
{
    public static Ticket Create(
        Guid? requesterId = null,
        Sector? sector = null,
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
            sector ?? CreateSector(),
            requester,
            assignee,
            slaPlan!,
            dueAtUtc,
            slaWarningAtUtc,
            requester.Id,
            openedAtUtc);

        if (status is TicketStatus.InProgress or TicketStatus.Resolved or TicketStatus.Closed)
        {
            ticket.Update(description, TicketStatus.InProgress, requester.Id, openedAtUtc);
        }

        if (status is TicketStatus.Resolved or TicketStatus.Closed)
        {
            ticket.Update(description, TicketStatus.Resolved, requester.Id, resolvedAtUtc ?? openedAtUtc);
        }

        if (status == TicketStatus.Closed)
        {
            ticket.Close(requester.Id, resolvedAtUtc ?? openedAtUtc);
        }

        if (status == TicketStatus.Canceled)
        {
            ticket.Update(description, TicketStatus.Canceled, requester.Id, openedAtUtc);
        }

        if (deletedAtUtc is { } deletedAt)
        {
            ticket.Delete(requester.Id, deletedAt);
        }

        ticket.ClearDomainEvents();

        return ticket;
    }

    // Setor num departamento novo, sem gravar; serve aos chamados cujo setor o teste não lê.
    public static Sector CreateSector() =>
        Sector.Create(Department.Create("Suporte", "Atendimento", responsibleUser: null), "Helpdesk", description: null);
}
