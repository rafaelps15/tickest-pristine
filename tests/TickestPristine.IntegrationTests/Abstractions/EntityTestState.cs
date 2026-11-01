namespace TickestPristine.IntegrationTests;

// Ajusta propriedades de setter privado para montar cenários que a API levaria tempo ou vários passos para produzir
// (datas no passado, prazos fixos, ids conhecidos); as mudanças de estado dos testes continuam vindo dos métodos da
// entidade.
internal static class EntityTestState
{
    // Instância pelo construtor privado, para o cenário que nenhum caminho da entidade monta direto.
    public static T New<T>()
        where T : class => (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;

    public static T With<T>(this T entity, string propertyName, object? value)
        where T : class
    {
        typeof(T).GetProperty(propertyName)!.SetValue(entity, value);

        return entity;
    }
}
