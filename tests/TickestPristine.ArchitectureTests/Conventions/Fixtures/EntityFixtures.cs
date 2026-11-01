using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions.Fixtures;

// Tipos de exemplo dos testes do RichEntitySetterChecker: nunca são persistidos nem usados fora dos testes.

public sealed class RichFixtureEntity : Entity
{
    private RichFixtureEntity(string name) => Name = name;

    public string Name { get; private set; }

    public static RichFixtureEntity Create(string name) => new(name);

    public void Rename(string name) => Name = name;
}

public sealed class PublicSetterFixtureEntity : Entity
{
    public string Name { get; set; } = string.Empty;
}

public sealed class InitSetterFixtureEntity : Entity
{
    public string Name { get; init; } = string.Empty;
}

public sealed class InternalSetterFixtureEntity : Entity
{
    public string Name { get; internal set; } = string.Empty;
}

public sealed class NotAnEntityFixture
{
    public string Name { get; private set; } = string.Empty;
}
