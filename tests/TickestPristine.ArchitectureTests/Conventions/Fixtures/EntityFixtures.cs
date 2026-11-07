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

// Tipos de exemplo dos testes do EntityModelChecker: o primeiro segue o modelo de entidade, e cada um dos outros viola
// um critério só.

public sealed class ModelFixtureEntity : Entity
{
    private readonly List<string> _labels = [];

    private ModelFixtureEntity()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public IReadOnlyCollection<string> Labels => _labels;

    public static ModelFixtureEntity Create(string name) => new() { Id = Guid.NewGuid(), Name = name };

    public void Rename(string name) => Name = name;

    public bool IsNamed(string name) => Name == name;
}

public class UnsealedModelFixtureEntity : Entity
{
    private UnsealedModelFixtureEntity()
    {
    }

    public Guid Id { get; private set; }

    public static UnsealedModelFixtureEntity Create() => new() { Id = Guid.NewGuid() };
}

public sealed class WithoutIdModelFixtureEntity : Entity
{
    private WithoutIdModelFixtureEntity()
    {
    }

    public string Code { get; private set; } = string.Empty;

    public static WithoutIdModelFixtureEntity Create(string code) => new() { Code = code };
}

public sealed class PublicConstructorModelFixtureEntity : Entity
{
    public PublicConstructorModelFixtureEntity(Guid id) => Id = id;

    public Guid Id { get; private set; }

    public static PublicConstructorModelFixtureEntity Create() => new(Guid.NewGuid());
}

public sealed class ComputedPropertyModelFixtureEntity : Entity
{
    private ComputedPropertyModelFixtureEntity()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool HasName => Name.Length > 0;

    public static ComputedPropertyModelFixtureEntity Create(string name) => new() { Id = Guid.NewGuid(), Name = name };
}

public sealed class PublicSetterModelFixtureEntity : Entity
{
    private PublicSetterModelFixtureEntity()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; set; } = string.Empty;

    public static PublicSetterModelFixtureEntity Create() => new() { Id = Guid.NewGuid() };
}

public sealed class InternalSetterModelFixtureEntity : Entity
{
    private InternalSetterModelFixtureEntity()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; internal set; } = string.Empty;

    public static InternalSetterModelFixtureEntity Create() => new() { Id = Guid.NewGuid() };
}

public sealed class WithoutCreateModelFixtureEntity : Entity
{
    private WithoutCreateModelFixtureEntity()
    {
    }

    public Guid Id { get; private set; }

    public WithoutCreateModelFixtureEntity Copy() => new() { Id = Id };
}

public sealed class PublicStaticModelFixtureEntity : Entity
{
    private PublicStaticModelFixtureEntity()
    {
    }

    public Guid Id { get; private set; }

    public static PublicStaticModelFixtureEntity Create() => new() { Id = Guid.NewGuid() };

    public static bool Matches(Guid id) => id != Guid.Empty;
}

public sealed class PublicConstModelFixtureEntity : Entity
{
    public const int MaxNameLength = 10;

    private PublicConstModelFixtureEntity()
    {
    }

    public Guid Id { get; private set; }

    public static PublicConstModelFixtureEntity Create() => new() { Id = Guid.NewGuid() };
}
