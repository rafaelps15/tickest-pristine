using System.Reflection;
using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions;

/// <summary>
/// Confere que as entidades declaradas ricas não têm propriedade com setter público (<c>set</c> ou
/// <c>init</c>): o estado delas só muda pelos próprios métodos. Um tipo da lista que não deriva de
/// <see cref="Entity"/> também é reportado, para que um engano na lista não passe em silêncio.
/// </summary>
internal static class RichEntitySetterChecker
{
    /// <summary>Devolve uma mensagem por propriedade com setter público e por tipo da lista que não é entidade.</summary>
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

            IEnumerable<PropertyInfo> publicSetters = entity
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(property => property.SetMethod?.IsPublic == true)
                .OrderBy(property => property.Name, StringComparer.Ordinal);

            messages.AddRange(publicSetters.Select(property =>
                $"{entity.FullName ?? entity.Name}.{property.Name} tem setter público: use private set e um método da entidade."));
        }

        return messages;
    }
}
