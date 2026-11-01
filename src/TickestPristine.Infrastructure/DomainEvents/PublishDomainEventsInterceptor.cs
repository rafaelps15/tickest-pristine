using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.SharedKernel;

namespace TickestPristine.Infrastructure.DomainEvents;

/// <summary>
/// Publica os eventos de domínio das entidades depois de um SaveChanges bem-sucedido, fora da transação; se o save
/// falhar, os eventos continuam nas entidades para uma nova tentativa.
/// </summary>
internal sealed class PublishDomainEventsInterceptor(IServiceScopeFactory serviceScopeFactory) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await PublishDomainEventsAsync(eventData.Context);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task PublishDomainEventsAsync(DbContext context)
    {
        var domainEvents = context
            .ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .SelectMany(entity =>
            {
                List<IDomainEvent> domainEvents = entity.DomainEvents;

                entity.ClearDomainEvents();

                return domainEvents;
            })
            .ToList();

        using IServiceScope scope = serviceScopeFactory.CreateScope();

        IDomainEventsDispatcher domainEventsDispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventsDispatcher>();

        await domainEventsDispatcher.DispatchAsync(domainEvents);
    }
}
