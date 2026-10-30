using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Sectors;

namespace TickestPristine.Application.UnitTests.Sectors;

public sealed class SectorTests
{
    private static readonly Department ParentDepartment = Department.Create("Suporte", "Atendimento ao cliente", responsibleUser: null);

    [Fact]
    public void Create_Should_CreateActiveSectorAndRaiseEvent_WhenDataIsValid()
    {
        // Arrange
        const string name = "Helpdesk";

        // Act
        var sector = Sector.Create(ParentDepartment, name, "Primeiro atendimento");

        // Assert
        sector.Id.ShouldNotBe(Guid.Empty);
        sector.DepartmentId.ShouldBe(ParentDepartment.Id);
        sector.Name.ShouldBe(name);
        sector.Description.ShouldBe("Primeiro atendimento");
        sector.IsActive.ShouldBeTrue();
        sector.DomainEvents.OfType<SectorCreatedDomainEvent>().ShouldContain(e => e.SectorId == sector.Id);
    }

    [Fact]
    public void Update_Should_ChangeFieldsAndRaiseEvent_WhenCalled()
    {
        // Arrange
        Sector sector = CreateSector();

        // Act
        sector.Update("Redes", description: null);

        // Assert
        sector.Name.ShouldBe("Redes");
        sector.Description.ShouldBeNull();
        sector.DomainEvents.OfType<SectorUpdatedDomainEvent>().ShouldContain(e => e.SectorId == sector.Id);
    }

    [Fact]
    public void Update_Should_RaiseEvent_WhenNothingChanged()
    {
        // Arrange
        Sector sector = CreateSector();

        // Act
        sector.Update(sector.Name, sector.Description);

        // Assert
        sector.DomainEvents.OfType<SectorUpdatedDomainEvent>().ShouldContain(e => e.SectorId == sector.Id);
    }

    [Fact]
    public void Deactivate_Should_DeactivateAndRaiseEvent_WhenSectorIsActive()
    {
        // Arrange
        Sector sector = CreateSector();

        // Act
        sector.Deactivate();

        // Assert
        sector.IsActive.ShouldBeFalse();
        sector.DomainEvents.OfType<SectorDeactivatedDomainEvent>().ShouldContain(e => e.SectorId == sector.Id);
    }

    private static Sector CreateSector() =>
        Sector.Create(ParentDepartment, "Helpdesk", "Primeiro atendimento");
}
