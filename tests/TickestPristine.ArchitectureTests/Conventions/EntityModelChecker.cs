using System.Reflection;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Confere as entidades contra o modelo da skill de entidade: <c>sealed</c> com <c>Id</c> do tipo <see cref="Guid"/>
/// (C1); um único construtor, privado e sem parâmetros (C2); nenhuma propriedade calculada nem setter não privado, só
/// <c>private set</c> ou uma coleção somente leitura (C3); um <c>Create</c> estático, público ou interno (C4); e nenhum
/// membro estático público além do <c>Create</c>, nem constante pública (C5). Devolve uma mensagem por desvio, montada
/// pelas mesmas funções que a lista de exceções dos testes usa.
/// </summary>
internal static class EntityModelChecker
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private const BindingFlags PublicInstanceDeclared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private const BindingFlags PublicStaticDeclared = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static IReadOnlyList<string> Check(IEnumerable<Type> entities) =>
        [.. entities.OrderBy(type => type.FullName, StringComparer.Ordinal).SelectMany(Deviations)];

    public static string NotSealed(Type entity) => $"{NameOf(entity)} não é sealed (C1).";

    public static string WithoutGuidId(Type entity) => $"{NameOf(entity)} não tem Id do tipo Guid (C1).";

    public static string WrongConstructor(Type entity) =>
        $"{NameOf(entity)} precisa de um único construtor, privado e sem parâmetros (C2).";

    public static IEnumerable<string> ComputedProperties(Type entity, params string[] properties) =>
        properties.Select(property =>
            $"{NameOf(entity)}.{property} é propriedade calculada: use private set ou um método de consulta (C3).");

    public static IEnumerable<string> NonPrivateSetters(Type entity, params string[] properties) =>
        properties.Select(property =>
            $"{NameOf(entity)}.{property} tem setter não privado: use private set e um método da entidade (C3).");

    public static string WithoutCreate(Type entity) => $"{NameOf(entity)} não tem um Create estático (C4).";

    public static IEnumerable<string> PublicStatics(Type entity, params string[] members) =>
        members.Select(member => $"{NameOf(entity)}.{member} é membro estático público além do Create (C5).");

    public static IEnumerable<string> PublicConstants(Type entity, params string[] fields) =>
        fields.Select(field => $"{NameOf(entity)}.{field} é constante pública (C5).");

    private static List<string> Deviations(Type entity)
    {
        var messages = new List<string>();

        if (!entity.IsSealed)
        {
            messages.Add(NotSealed(entity));
        }

        if (entity.GetProperty("Id")?.PropertyType != typeof(Guid))
        {
            messages.Add(WithoutGuidId(entity));
        }

        if (!HasSinglePrivateParameterlessConstructor(entity))
        {
            messages.Add(WrongConstructor(entity));
        }

        messages.AddRange(ComputedProperties(entity, ComputedPropertyNames(entity)));
        messages.AddRange(NonPrivateSetters(entity, NonPrivateSetterNames(entity)));

        if (!HasCreate(entity))
        {
            messages.Add(WithoutCreate(entity));
        }

        messages.AddRange(PublicStatics(entity, PublicStaticMemberNames(entity)));
        messages.AddRange(PublicConstants(entity, PublicConstantNames(entity)));

        return messages;
    }

    private static bool HasSinglePrivateParameterlessConstructor(Type entity) =>
        entity.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) is
            [{ IsPrivate: true } constructor] && constructor.GetParameters().Length == 0;

    // A coleção num campo privado é exposta como IReadOnlyCollection<T>, sem setter, como o modelo pede.
    private static string[] ComputedPropertyNames(Type entity) =>
    [
        .. entity
            .GetProperties(PublicInstanceDeclared)
            .Where(property => property.SetMethod is null && !IsReadOnlyCollection(property.PropertyType))
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
    ];

    private static string[] NonPrivateSetterNames(Type entity) =>
    [
        .. entity
            .GetProperties(Declared)
            .Where(property => property.SetMethod is { IsPrivate: false })
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
    ];

    private static bool HasCreate(Type entity) =>
        entity
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Any(method => method.Name == "Create" && (method.IsPublic || method.IsAssembly));

    private static string[] PublicStaticMemberNames(Type entity)
    {
        IEnumerable<string> methods = entity
            .GetMethods(PublicStaticDeclared)
            .Where(method => method.Name != "Create" && !method.IsSpecialName)
            .Select(method => method.Name);

        IEnumerable<string> properties = entity.GetProperties(PublicStaticDeclared).Select(property => property.Name);

        IEnumerable<string> fields = entity
            .GetFields(PublicStaticDeclared)
            .Where(field => !field.IsLiteral)
            .Select(field => field.Name);

        return [.. methods.Concat(properties).Concat(fields).Distinct().Order(StringComparer.Ordinal)];
    }

    private static string[] PublicConstantNames(Type entity) =>
    [
        .. entity
            .GetFields(PublicStaticDeclared)
            .Where(field => field.IsLiteral)
            .Select(field => field.Name)
            .Order(StringComparer.Ordinal)
    ];

    private static bool IsReadOnlyCollection(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>);

    private static string NameOf(Type entity) => entity.FullName ?? entity.Name;
}
