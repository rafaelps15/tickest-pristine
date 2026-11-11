namespace TickestPristine.SharedKernel;

/// <summary>Regra de negócio com nome próprio, conferida pelo agregado antes de alterar estado.</summary>
public interface IBusinessRule
{
    bool IsBroken();

    Error Error { get; }
}
