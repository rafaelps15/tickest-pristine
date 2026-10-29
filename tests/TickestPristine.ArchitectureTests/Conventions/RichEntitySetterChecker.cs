using System.Reflection;
using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Confere que as entidades declaradas ricas só têm setters privados (nenhum <c>set</c> ou <c>init</c> público,
/// interno ou protegido): o estado delas só muda pelos próprios métodos. Um tipo da lista que não deriva de
/// <see cref="Entity"/> também é reportado, para que um engano na lista não passe em silêncio.
/// </summary>
internal static class RichEntitySetterChecker
{
    public static IReadOnlyList<string> Check(IEnumerable<Type> richEntities)
    {
        var messages = new List<string>();

        foreach (Type entity in richEntities.OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            if (!entity.IsSubclassOf(typeof(Entity)))
            {
                messages.Add($"{entity.FullName ?? entity.Name} está na lista de entidades ricas, mas não deriva de Entity.");
                continue;
            }

            IEnumerable<PropertyInfo> nonPrivateSetters = entity
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(property => property.SetMethod is { IsPrivate: false })
                .OrderBy(property => property.Name, StringComparer.Ordinal);

            messages.AddRange(nonPrivateSetters.Select(property =>
                $"{entity.FullName ?? entity.Name}.{property.Name} tem setter não privado: use private set e um método da entidade."));
        }

        return messages;
    }
}
