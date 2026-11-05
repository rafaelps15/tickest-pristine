namespace TickestPristine.SharedKernel;

/// <summary>
/// Base dos objetos de valor: dois objetos são iguais quando são do mesmo tipo e têm os mesmos valores atômicos
/// (<see cref="GetAtomicValues"/>), sem identidade própria.
/// </summary>
#pragma warning disable S4035 // Base abstrata: a igualdade já confere o tipo concreto antes dos valores atômicos.
public abstract class ValueObject : IEquatable<ValueObject>
#pragma warning restore S4035
{
    public static bool operator ==(ValueObject? a, ValueObject? b) =>
        a is null ? b is null : a.Equals(b);

    public static bool operator !=(ValueObject? a, ValueObject? b) => !(a == b);

    public bool Equals(ValueObject? other) =>
        other is not null && GetType() == other.GetType() && GetAtomicValues().SequenceEqual(other.GetAtomicValues());

    public override bool Equals(object? obj) => obj is ValueObject other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hashCode = default;

        foreach (object value in GetAtomicValues())
        {
            hashCode.Add(value);
        }

        return hashCode.ToHashCode();
    }

    protected abstract IEnumerable<object> GetAtomicValues();
}
