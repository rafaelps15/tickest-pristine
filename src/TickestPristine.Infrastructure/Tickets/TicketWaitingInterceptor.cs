using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TickestPristine.Application.Permissions;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Infrastructure.Tickets;

/// <summary>
/// Mantém a situação de espera do chamado (<see cref="Ticket.WaitingOn"/>): a cada mensagem nova ou alterada (inclusive
/// a exclusão lógica) e a cada chamado criado ou com o status alterado, recalcula a situação no banco depois do commit.
/// Fica num lugar só para que nenhum handler esqueça de atualizá-la (spec 029).
/// </summary>
/// <remarks>
/// A regra: chamado inativo não tem situação; ativo aguarda o solicitante quando a última mensagem válida é de quem
/// atende (autor que não é o solicitante, ativo e com <c>tickets:manage</c> em alguma role, o mesmo critério do
/// <c>PermissionProvider</c>), e quem atende nos demais casos, inclusive sem mensagens. A permissão é a do momento do
/// recálculo (DEC-4). O <c>UPDATE</c> roda depois do commit, sem checagem de versão, para duas mensagens simultâneas
/// não conflitarem (como a última atividade, spec 015, DEC-11); o chamado que estiver no contexto é recarregado, para
/// não ficar com a versão antiga.
/// </remarks>
internal sealed class TicketWaitingInterceptor : SaveChangesInterceptor
{
    // Chamados a recalcular depois do commit, por contexto: o mesmo interceptor pode servir a mais de um contexto.
    private readonly ConditionalWeakTable<DbContext, HashSet<Guid>> _pending = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Prepare(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Prepare(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && _pending.TryGetValue(context, out HashSet<Guid>? ticketIds))
        {
            _pending.Remove(context);

            foreach (Guid ticketId in ticketIds)
            {
                await context.Database.ExecuteSqlAsync(RecalculateSql(ticketId), cancellationToken);
            }

            foreach (EntityEntry<Ticket> entry in TrackedTickets(context, ticketIds))
            {
                await entry.ReloadAsync(cancellationToken);
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } context && _pending.TryGetValue(context, out HashSet<Guid>? ticketIds))
        {
            _pending.Remove(context);

            foreach (Guid ticketId in ticketIds)
            {
                context.Database.ExecuteSql(RecalculateSql(ticketId));
            }

            foreach (EntityEntry<Ticket> entry in TrackedTickets(context, ticketIds))
            {
                entry.Reload();
            }
        }

        return base.SavedChanges(eventData, result);
    }

    // Sem commit, não há o que recalcular; uma nova tentativa de SaveChanges refaz a lista.
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Discard(eventData.Context);

        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <summary>
    /// Guarda os chamados afetados por este SaveChanges: mensagem nova ou alterada, chamado criado ou com status alterado.
    /// </summary>
    private void Prepare(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();

        var ticketIds = context.ChangeTracker.Entries<TicketMessage>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity.TicketId)
            .ToHashSet();

        foreach (EntityEntry<Ticket> entry in context.ChangeTracker.Entries<Ticket>())
        {
            if (entry.State == EntityState.Added ||
                entry.State == EntityState.Modified && entry.Property(t => t.Status).IsModified)
            {
                ticketIds.Add(entry.Entity.Id);
            }
        }

        _pending.AddOrUpdate(context, ticketIds);
    }

    private void Discard(DbContext? context)
    {
        if (context is not null)
        {
            _pending.Remove(context);
        }
    }

    private static List<EntityEntry<Ticket>> TrackedTickets(DbContext context, HashSet<Guid> ticketIds) =>
        [.. context.ChangeTracker.Entries<Ticket>().Where(entry => ticketIds.Contains(entry.Entity.Id))];

    private static FormattableString RecalculateSql(Guid ticketId) =>
        $"""
        UPDATE public.tickets AS t
        SET waiting_on = CASE
            WHEN t.status NOT IN ({nameof(TicketStatus.Open)}, {nameof(TicketStatus.InProgress)}) THEN NULL
            WHEN COALESCE((
                SELECT m.author_user_id <> t.created_by_user_id AND EXISTS (
                    SELECT 1
                    FROM public.users AS u
                    JOIN public.user_roles AS ur ON ur.user_id = u.id
                    JOIN public.role_permissions AS rp ON rp.role_name = ur.role_name
                    WHERE u.id = m.author_user_id
                      AND u.is_active
                      AND rp.permission_code = {PermissionCodes.Tickets.Manage})
                FROM public.ticket_messages AS m
                WHERE m.ticket_id = t.id AND m.deleted_at_utc IS NULL
                ORDER BY m.created_at_utc DESC, m.id DESC
                LIMIT 1), false) THEN {nameof(TicketWaitingOn.Requester)}
            ELSE {nameof(TicketWaitingOn.Agent)}
        END
        WHERE t.id = {ticketId}
        """;
}
