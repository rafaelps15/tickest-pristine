using System.Reflection;
using System.Runtime.CompilerServices;
using NetArchTest.Rules;
using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Regras das consultas em SQL: os slices de query leem o banco só pela <see cref="IDbConnectionFactory"/>, sem o
/// <see cref="IApplicationDbContext"/>, e nenhuma classe estática estende consultas do EF sobre as entidades
/// informadas. O slice é o namespace do query handler, fora dos decorators de <c>Abstractions</c>; sem nenhum query
/// handler na raiz informada, a seleção dos tipos é reportada.
/// </summary>
internal static class QuerySliceChecker
{
    public static IReadOnlyList<string> CheckDataAccess(Assembly assembly, string rootNamespace)
    {
        string[] slices = [.. assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && ImplementsQueryHandler(type))
            .Select(type => type.Namespace)
            .OfType<string>()
            .Where(name => name.StartsWith(rootNamespace + ".", StringComparison.Ordinal) &&
                           !name.Contains(".Abstractions", StringComparison.Ordinal))
            .Distinct()
            .Order(StringComparer.Ordinal)];

        if (slices.Length == 0)
        {
            return [$"Nenhum query handler encontrado em {rootNamespace}: a seleção dos tipos pode estar errada."];
        }

        var messages = new List<string>();

        foreach (string slice in slices)
        {
            TestResult result = Types.InAssembly(assembly)
                .That()
                .ResideInNamespace(slice)
                .Should()
                .NotHaveDependencyOn(typeof(IApplicationDbContext).FullName)
                .GetResult();

            // O filtro de namespace do NetArchTest é por prefixo: confere o namespace de cada tipo encontrado.
            messages.AddRange((result.FailingTypes ?? [])
                .Where(type => type.Namespace == slice)
                .Select(type => $"{type.FullName} lê pelo IApplicationDbContext num slice de query"));
        }

        return messages;
    }

    public static IReadOnlyList<string> CheckQueryableExtensions(
        Assembly assembly,
        string rootNamespace,
        IReadOnlyCollection<Type> entityTypes)
    {
        Type[] queryableTypes = [typeof(IQueryable<>), typeof(IOrderedQueryable<>)];

        return [.. assembly.GetTypes()
            .Where(type => type is { IsAbstract: true, IsSealed: true } &&
                           (type.Namespace ?? string.Empty).StartsWith(rootNamespace, StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(method => method.IsDefined(typeof(ExtensionAttribute), inherit: false))
            .Where(method =>
            {
                Type extended = method.GetParameters()[0].ParameterType;

                return extended.IsGenericType &&
                       queryableTypes.Contains(extended.GetGenericTypeDefinition()) &&
                       entityTypes.Contains(extended.GetGenericArguments()[0]);
            })
            .Select(method => $"{method.DeclaringType!.FullName}.{method.Name} estende consultas do EF")
            .Order(StringComparer.Ordinal)];
    }

    private static bool ImplementsQueryHandler(Type type) =>
        type.GetInterfaces().Any(contract =>
            contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IQueryHandler<,>));
}
