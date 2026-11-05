using System.Reflection;
using Shouldly;
using TickestPristine.SharedKernel;

namespace TickestPristine.ArchitectureTests.Conventions;

public class EntityBaseTests : BaseTest
{
    [Fact]
    public void DomainEvents_Should_BeReadOnlyCollection_When_EntityIsInspected()
    {
        // Arrange
        PropertyInfo? property = typeof(Entity).GetProperty(nameof(Entity.DomainEvents));

        // Act
        Type? type = property?.PropertyType;

        // Assert
        type.ShouldBe(typeof(IReadOnlyCollection<IDomainEvent>));
        property!.CanWrite.ShouldBeFalse();
    }
}
