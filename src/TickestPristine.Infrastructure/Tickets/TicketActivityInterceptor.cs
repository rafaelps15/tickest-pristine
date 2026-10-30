using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.Infrastructure.Tickets;

/// <summary>
/// Mantém a última atividade do chamado: a cada registro novo de histórico, leva o <see cref="Ticket.LastActivityAtUtc"/>
/// do chamado à data do registro, sem nunca diminuí-la. Fica num lugar só para que nenhum handler que grave histórico
/// esqueça de atualizá-la.
/// </summary>
/// <remarks>
/// O chamado criado ou alterado no mesmo SaveChanges recebe a data na própria entidade. Os demais (mensagens, anexos,
/// chamado só lido) são atualizados por um <c>UPDATE</c> depois do commit, sem checagem de versão: assim uma mensagem
/// não altera a versão do chamado dentro da transação dela, e duas mensagens simultâneas não conflitam.
/// </remarks>
internal sealed class TicketActivityInterceptor : SaveChangesInterceptor
{
    // Atividades a gravar depois do commit, por contexto: o mesmo interceptor pode servir a mais de um contexto.
    private readonly ConditionalWeakTable<DbContext, List<(Guid TicketId, DateTime OccurredAtUtc)>> _pending = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Prepare(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // Cobre quem chama o SaveChanges síncrono, como no interceptor de auditoria.
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
        if (eventData.Context is { } context && _pending.TryGetValue(context, out List<(Guid TicketId, DateTime OccurredAtUtc)>? pending))
        {
            _pending.Remove(context);

            foreach ((Guid ticketId, DateTime occurredAtUtc) in pending)
            {
                await context.Database.ExecuteSqlAsync(MoveForwardSql(ticketId, occurredAtUtc), cancellationToken);
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } context && _pending.TryGetValue(context, out List<(Guid TicketId, DateTime OccurredAtUtc)>? pending))
        {
            _pending.Remove(context);

            foreach ((Guid ticketId, DateTime occurredAtUtc) in pending)
            {
                context.Database.ExecuteSql(MoveForwardSql(ticketId, occurredAtUtc));
            }
        }

        return base.SavedChanges(eventData, result);
    }

    // Sem commit, não há atividade a gravar; uma nova tentativa de SaveChanges recalcula a lista.
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
    /// Leva a data aos chamados criados ou alterados neste SaveChanges e guarda os demais para depois do commit.
    /// </summary>
    private void Prepare(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();

        var latestByTicket = context.ChangeTracker.Entries<TicketHistory>()
            .Where(entry => entry.State == EntityState.Added)
            .GroupBy(entry => entry.Entity.TicketId)
            .Select(group => (TicketId: group.Key, OccurredAtUtc: group.Max(entry => entry.Entity.OccurredAtUtc)))
            .ToList();

        var afterCommit = new List<(Guid TicketId, DateTime OccurredAtUtc)>();

        foreach ((Guid ticketId, DateTime occurredAtUtc) in latestByTicket)
        {
            EntityEntry<Ticket>? changedTicket = context.ChangeTracker.Entries<Ticket>()
                .FirstOrDefault(entry => entry.State is EntityState.Added or EntityState.Modified && entry.Entity.Id == ticketId);

            if (changedTicket is null)
            {
                afterCommit.Add((ticketId, occurredAtUtc));
            }
            else if (changedTicket.Entity.LastActivityAtUtc < occurredAtUtc)
            {
                // A data é da infraestrutura, e não de uma regra do chamado: entra pela API do EF.
                changedTicket.Property(ticket => ticket.LastActivityAtUtc).CurrentValue = occurredAtUtc;
            }
        }

        _pending.AddOrUpdate(context, afterCommit);
    }

    private void Discard(DbContext? context)
    {
        if (context is not null)
        {
            _pending.Remove(context);
        }
    }

    // GREATEST: a data nunca volta, mesmo com duas ações gravadas fora de ordem.
    private static FormattableString MoveForwardSql(Guid ticketId, DateTime occurredAtUtc) =>
        $"""
        UPDATE public.tickets
        SET last_activity_at_utc = GREATEST(last_activity_at_utc, {occurredAtUtc})
        WHERE id = {ticketId}
        """;
}
