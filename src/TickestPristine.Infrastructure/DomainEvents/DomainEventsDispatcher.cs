using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TickestPristine.SharedKernel;

namespace TickestPristine.Infrastructure.DomainEvents;

/// <summary>
/// Publica os eventos depois do SaveChanges, fora da transação: a mudança já foi gravada. Por isso a falha de um
/// handler é registrada e não interrompe os demais nem a resposta da requisição, que já foi bem-sucedida.
/// </summary>
internal sealed class DomainEventsDispatcher(
    IServiceProvider serviceProvider,
    ILogger<DomainEventsDispatcher> logger) : IDomainEventsDispatcher
{
    private static readonly ConcurrentDictionary<Type, Type> HandlerTypeDictionary = new();
    private static readonly ConcurrentDictionary<Type, Type> WrapperTypeDictionary = new();

    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (IDomainEvent domainEvent in domainEvents)
        {
            using IServiceScope scope = serviceProvider.CreateScope();

            Type domainEventType = domainEvent.GetType();
            Type handlerType = HandlerTypeDictionary.GetOrAdd(
                domainEventType,
                et => typeof(IDomainEventHandler<>).MakeGenericType(et));

            IEnumerable<object?> handlers = scope.ServiceProvider.GetServices(handlerType);

            foreach (object? handler in handlers)
            {
                if (handler is null)
                {
                    continue;
                }

                var handlerWrapper = HandlerWrapper.Create(handler, domainEventType);

#pragma warning disable CA1031 // A mudança já foi salva: a falha de um handler é registrada sem derrubar a requisição.
                try
                {
                    await handlerWrapper.Handle(domainEvent, cancellationToken);
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Domain event handler {Handler} failed for {DomainEvent}",
                        handler.GetType().Name,
                        domainEventType.Name);
                }
#pragma warning restore CA1031
            }
        }
    }

    private abstract class HandlerWrapper
    {
        public abstract Task Handle(IDomainEvent domainEvent, CancellationToken cancellationToken);

        public static HandlerWrapper Create(object handler, Type domainEventType)
        {
            Type wrapperType = WrapperTypeDictionary.GetOrAdd(
                domainEventType,
                et => typeof(HandlerWrapper<>).MakeGenericType(et));

            return (HandlerWrapper)Activator.CreateInstance(wrapperType, handler);
        }
    }

    private sealed class HandlerWrapper<T>(object handler) : HandlerWrapper where T : IDomainEvent
    {
        private readonly IDomainEventHandler<T> _handler = (IDomainEventHandler<T>)handler;

        public override async Task Handle(IDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            await _handler.Handle((T)domainEvent, cancellationToken);
        }
    }
}
