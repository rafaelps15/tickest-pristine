using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Confere o nome e a pasta de cada handler de evento de domínio: <c>{Ação}On{Evento}DomainEventHandler</c>, num
/// namespace que termina com o nome do evento sem o sufixo <c>DomainEvent</c> (a pasta do evento). Um handler que trata
/// mais de um evento é reportado, porque o nome só descreve um.
/// </summary>
internal static class DomainEventHandlerNameChecker
{
    private const string EventSuffix = "DomainEvent";

    public static IReadOnlyList<string> Check(IEnumerable<Type> types) =>
        [.. types
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Select(type => (Type: type, Events: HandledEvents(type)))
            .Where(handler => handler.Events.Length > 0)
            .SelectMany(handler => Check(handler.Type, handler.Events))
            .Order(StringComparer.Ordinal)];

    private static IEnumerable<string> Check(Type handler, Type[] events)
    {
        if (events.Length > 1)
        {
            yield return $"{handler.FullName}: trata mais de um evento";
            yield break;
        }

        string eventName = events[0].Name.EndsWith(EventSuffix, StringComparison.Ordinal)
            ? events[0].Name[..^EventSuffix.Length]
            : events[0].Name;

        string expectedSuffix = $"On{eventName}{EventSuffix}Handler";

        if (!handler.Name.EndsWith(expectedSuffix, StringComparison.Ordinal) || handler.Name.Length == expectedSuffix.Length)
        {
            yield return $"{handler.FullName}: o nome deve ser {{Ação}}{expectedSuffix}";
        }

        if (handler.Namespace is null || !handler.Namespace.EndsWith($".{eventName}", StringComparison.Ordinal))
        {
            yield return $"{handler.FullName}: a pasta (namespace) deve terminar em .{eventName}";
        }
    }

    private static Type[] HandledEvents(Type type) =>
        [.. type.GetInterfaces()
            .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
            .Select(contract => contract.GetGenericArguments()[0])];
}
