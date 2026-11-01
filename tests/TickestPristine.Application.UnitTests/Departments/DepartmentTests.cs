using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Users;

namespace TickestPristine.Application.UnitTests.Departments;

public sealed class DepartmentTests
{
    private static readonly User ResponsibleUser = new() { Id = Guid.NewGuid() };

    [Fact]
    public void Create_Should_CreateActiveDepartmentAndRaiseEvent_WhenDataIsValid()
    {
        // Arrange
        const string name = "Suporte";

        // Act
        var department = Department.Create(name, "Atendimento ao cliente", ResponsibleUser);

        // Assert
        department.Id.ShouldNotBe(Guid.Empty);
        department.Name.ShouldBe(name);
        department.Description.ShouldBe("Atendimento ao cliente");
        department.ResponsibleUserId.ShouldBe(ResponsibleUser.Id);
        department.IsActive.ShouldBeTrue();
        department.DomainEvents.OfType<DepartmentCreatedDomainEvent>().ShouldContain(e => e.DepartmentId == department.Id);
    }

    [Fact]
    public void Update_Should_ChangeFieldsAndRaiseEvent_WhenCalled()
    {
        // Arrange
        Department department = CreateDepartment();
        var newResponsibleUser = new User { Id = Guid.NewGuid() };

        // Act
        department.Update("Financeiro", "Contas a pagar e a receber", newResponsibleUser);

        // Assert
        department.Name.ShouldBe("Financeiro");
        department.Description.ShouldBe("Contas a pagar e a receber");
        department.ResponsibleUserId.ShouldBe(newResponsibleUser.Id);
        department.DomainEvents.OfType<DepartmentUpdatedDomainEvent>().ShouldContain(e => e.DepartmentId == department.Id);
    }

    [Fact]
    public void Update_Should_RaiseEvent_WhenNothingChanged()
    {
        // Arrange
        Department department = CreateDepartment();

        // Act
        department.Update(department.Name, department.Description, ResponsibleUser);

        // Assert
        department.DomainEvents.OfType<DepartmentUpdatedDomainEvent>().ShouldContain(e => e.DepartmentId == department.Id);
    }

    [Fact]
    public void Deactivate_Should_DeactivateAndRaiseEvent_WhenDepartmentIsActive()
    {
        // Arrange
        Department department = CreateDepartment();

        // Act
        department.Deactivate();

        // Assert
        department.IsActive.ShouldBeFalse();
        department.DomainEvents.OfType<DepartmentDeactivatedDomainEvent>().ShouldContain(e => e.DepartmentId == department.Id);
    }

    private static Department CreateDepartment() =>
        Department.Create("Suporte", "Atendimento ao cliente", ResponsibleUser);
}
