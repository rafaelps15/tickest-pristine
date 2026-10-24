using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Confere que todo tipo concreto que implementa as interfaces de handler (command, query e evento de domínio,
/// inclusive os decorators) é <c>internal</c> (ou aninhado com visibilidade de assembly) e <c>sealed</c>.
/// Uma família sem nenhum tipo também é reportada, para que um filtro errado não passe em silêncio.
/// </summary>
internal static class HandlerShapeChecker
{
    private static readonly (string Family, Type[] Interfaces)[] Families =
    [
        ("handler de command", [typeof(ICommandHandler<>), typeof(ICommandHandler<,>)]),
        ("handler de query", [typeof(IQueryHandler<,>)]),
        ("handler de evento de domínio", [typeof(IDomainEventHandler<>)])
    ];

    public static IReadOnlyList<string> Check(IEnumerable<Type> types)
    {
        Type[] concreteTypes = types.Where(type => type.IsClass && !type.IsAbstract).ToArray();
        var messages = new List<string>();
        var handlers = new HashSet<Type>();

        foreach ((string family, Type[] interfaces) in Families)
        {
            Type[] familyTypes = concreteTypes.Where(type => Implements(type, interfaces)).ToArray();

            if (familyTypes.Length == 0)
            {
                messages.Add($"Nenhum tipo encontrado da família {family}: a seleção dos tipos pode estar errada.");
            }

            handlers.UnionWith(familyTypes);
        }

        foreach (Type handler in handlers.OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            bool isInternal = handler.IsNested ? handler.IsNestedAssembly : handler.IsNotPublic;

            string? missing = (isInternal, handler.IsSealed) switch
            {
                (false, false) => "internal e sealed",
                (false, true) => "internal",
                (true, false) => "sealed",
                _ => null
            };

            if (missing is not null)
            {
                messages.Add($"{handler.FullName ?? handler.Name} não é internal sealed: falta {missing}.");
            }
        }

        return messages;
    }

    private static bool Implements(Type type, Type[] openInterfaces) =>
        type.GetInterfaces().Any(implemented =>
            implemented.IsGenericType && openInterfaces.Contains(implemented.GetGenericTypeDefinition()));
}
