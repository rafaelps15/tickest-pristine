namespace TickestPristine.SharedKernel;

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => [.. _domainEvents];

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    /// <summary>Devolve a falha da regra quando ela está quebrada, sem lançar exceção.</summary>
    protected static Result CheckRule(IBusinessRule rule) =>
        rule.IsBroken() ? Result.Failure(rule.Error) : Result.Success();

    public void Raise(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
}

