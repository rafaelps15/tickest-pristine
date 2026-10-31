using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.Domain.SlaCalendars;

namespace TickestPristine.Application.UnitTests.Abstractions;

// Monta chamados pelos métodos de Ticket: abre com o Create e leva ao status pedido pelo caminho
// que a API permite (Em andamento, Resolvido, Fechado ou Cancelado), com a exclusão no fim, se pedida. As entradas do
// histórico e os eventos do arranjo são descartados, para o teste ver só o que o handler produz. Sem plano informado, o
// chamado recebe o legado; os prazos, a última atividade e a situação de espera ficam como o teste os pede (prazos
// zerados, a abertura e sem situação, quando não informados), para o cenário não depender do calendário.
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
        DateTime? deletedAtUtc = null,
        BusinessCalendar? calendar = null)
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
            slaPlan ?? TicketSlaPlan.Legacy(priority),
            calendar ?? ContinuousSlaCalendar.Create(),
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
            .With(nameof(Ticket.LastActivityAtUtc), openedAtUtc)
            .With(nameof(Ticket.WaitingOn), null);

        ticket.ClearDomainEvents();

        return ticket;
    }

    // Mensagem publicada pelo Ticket.PostMessage e, se pedido, excluída pelo autor; o id pode ser fixado para os
    // testes de ordenação. Os eventos do arranjo são descartados.
    public static TicketMessage CreateMessage(
        Guid ticketId,
        Guid authorUserId,
        string content,
        DateTime createdAtUtc,
        DateTime? deletedAtUtc = null,
        Guid? id = null)
    {
        // O chamado só dá o id: os testes de mensagem não precisam dele gravado.
        TicketMessage message = Create().PostMessage(authorUserId, content, authorIsAgent: false, createdAtUtc).Message
            .With(nameof(TicketMessage.TicketId), ticketId);

        if (deletedAtUtc is { } deletedAt)
        {
            message.Delete(authorUserId, canDeleteAny: false, deletedAt);
        }

        if (id is { } messageId)
        {
            message.With(nameof(TicketMessage.Id), messageId);
        }

        message.ClearDomainEvents();

        return message;
    }

    // Anexo PDF criado pelo TicketAttachment.Create, excluído e apagado do armazenamento se pedido. O tamanho é ajustado
    // depois da criação, para os testes de limite do chamado poderem somar arquivos maiores que o máximo de um anexo.
    // Os eventos do arranjo são descartados.
    public static TicketAttachment CreateAttachment(
        Guid ticketId,
        Guid uploadedByUserId,
        DateTime uploadedAtUtc,
        string fileName = "report.pdf",
        long fileSizeBytes = 1024,
        string storageKey = "storage-key.pdf",
        DateTime? deletedAtUtc = null,
        DateTime? purgedAtUtc = null)
    {
        TicketAttachment attachment = TicketAttachment.Create(
            Create(),
            uploadedByUserId,
            fileName,
            "application/pdf",
            fileSizeBytes: 1,
            storageKey,
            currentCount: 0,
            currentTotalBytes: 0,
            uploadedAtUtc).Value;

        attachment
            .With(nameof(TicketAttachment.TicketId), ticketId)
            .With(nameof(TicketAttachment.FileSizeBytes), fileSizeBytes);

        if (deletedAtUtc is { } deletedAt)
        {
            attachment.Delete(uploadedByUserId, canDeleteAny: false, deletedAt);
        }

        if (purgedAtUtc is { } purgedAt)
        {
            attachment.MarkPurged(purgedAt);
        }

        attachment.ClearDomainEvents();

        return attachment;
    }

    // Setor num departamento novo, sem gravar; serve aos chamados cujo setor o teste não lê.
    public static Sector CreateSector() =>
        Sector.Create(Department.Create("Suporte", "Atendimento", responsibleUser: null), "Helpdesk", description: null);
}
