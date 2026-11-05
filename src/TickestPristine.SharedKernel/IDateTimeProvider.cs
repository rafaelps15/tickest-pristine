namespace TickestPristine.SharedKernel;

/// <summary>
/// Relógio do sistema, em UTC: a Application lê a hora por aqui, e não por <c>DateTime.UtcNow</c>, para que os
/// testes possam fixá-la.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
