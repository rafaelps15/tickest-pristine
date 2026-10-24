using FluentValidation;
using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Confere que todo command (tipo concreto que implementa <c>ICommand</c> ou <c>ICommand&lt;T&gt;</c>) tem um
/// validator: um tipo concreto que herda, direta ou indiretamente, de <c>AbstractValidator</c> desse command.
/// A ausência de commands também é reportada, para que um filtro errado não passe em silêncio.
/// </summary>
internal static class CommandValidatorChecker
{
    public static IReadOnlyList<string> Check(IEnumerable<Type> types)
    {
        Type[] concreteTypes = types.Where(type => !type.IsAbstract && !type.IsInterface).ToArray();
        Type[] commands = concreteTypes.Where(IsCommand).ToArray();

        if (commands.Length == 0)
        {
            return ["Nenhum command encontrado: a seleção dos tipos pode estar errada."];
        }

        var validatedTypes = concreteTypes.Select(ValidatedType).OfType<Type>().ToHashSet();

        return commands
            .Where(command => !validatedTypes.Contains(command))
            .OrderBy(command => command.FullName, StringComparer.Ordinal)
            .Select(command => $"{command.FullName} não tem validator (AbstractValidator<{command.Name}>).")
            .ToList();
    }

    private static bool IsCommand(Type type) =>
        type.GetInterfaces().Any(implemented =>
            implemented == typeof(ICommand) ||
            implemented.IsGenericType && implemented.GetGenericTypeDefinition() == typeof(ICommand<>));

    private static Type? ValidatedType(Type type)
    {
        for (Type? baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            {
                return baseType.GetGenericArguments()[0];
            }
        }

        return null;
    }
}
