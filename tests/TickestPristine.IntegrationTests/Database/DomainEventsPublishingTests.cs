using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Domain.Departments;
using TickestPristine.Infrastructure.Database;
using TickestPristine.Infrastructure.DomainEvents;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.Database;

/// <summary>
/// Valida a publicação dos eventos de domínio pelo <see cref="ApplicationDbContext"/> em todos os caminhos de gravação,
/// contra o PostgreSQL de verdade: os eventos só saem das entidades depois de um commit bem-sucedido.
/// </summary>
public sealed class DomainEventsPublishingTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task SaveChangesAsync_Should_PublishDomainEvents_WhenChangesAreSaved()
    {
        // Arrange
        var dispatcher = new RecordingDomainEventsDispatcher();
        using IServiceScope scope = Services.CreateScope();
        await using ApplicationDbContext context = CreateContext(scope, dispatcher);
        Department department = AddDepartment(context);

        // Act
        await context.SaveChangesAsync();

        // Assert
        dispatcher.Published.ShouldHaveSingleItem().ShouldBe(new DepartmentCreatedDomainEvent(department.Id));
        department.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_Should_PublishDomainEvents_WhenAcceptAllChangesIsFalse()
    {
        // Arrange
        var dispatcher = new RecordingDomainEventsDispatcher();
        using IServiceScope scope = Services.CreateScope();
        await using ApplicationDbContext context = CreateContext(scope, dispatcher);
        Department department = AddDepartment(context);

        // Act
        await context.SaveChangesAsync(acceptAllChangesOnSuccess: false);

        // Assert
        dispatcher.Published.ShouldHaveSingleItem().ShouldBe(new DepartmentCreatedDomainEvent(department.Id));
    }

    [Fact]
    public void SaveChanges_Should_PublishDomainEvents_WhenSavedSynchronously()
    {
        // Arrange
        // Teste síncrono de propósito: confere o caminho do SaveChanges sem async.
        var dispatcher = new RecordingDomainEventsDispatcher();
        using IServiceScope scope = Services.CreateScope();
        using ApplicationDbContext context = CreateContext(scope, dispatcher);
        Department department = AddDepartment(context);

        // Act
        context.SaveChanges();

        // Assert
        dispatcher.Published.ShouldHaveSingleItem().ShouldBe(new DepartmentCreatedDomainEvent(department.Id));
    }

    [Fact]
    public async Task SaveChangesAsync_Should_KeepDomainEventsAndPublishNothing_WhenSaveFails()
    {
        // Arrange
        // O nome de departamento ativo é único: o segundo com o mesmo nome falha no banco.
        string name = $"Departamento {Guid.NewGuid():N}";

        using (IServiceScope seedScope = Services.CreateScope())
        {
            await using ApplicationDbContext seedContext = CreateContext(seedScope, new RecordingDomainEventsDispatcher());
            AddDepartment(seedContext, name);
            await seedContext.SaveChangesAsync();
        }

        var dispatcher = new RecordingDomainEventsDispatcher();
        using IServiceScope scope = Services.CreateScope();
        await using ApplicationDbContext context = CreateContext(scope, dispatcher);
        Department duplicate = AddDepartment(context, name);

        // Act
        Func<Task> save = () => context.SaveChangesAsync();

        // Assert
        await save.ShouldThrowAsync<DbUpdateException>();
        dispatcher.Published.ShouldBeEmpty();
        duplicate.DomainEvents.ShouldHaveSingleItem().ShouldBe(new DepartmentCreatedDomainEvent(duplicate.Id));
    }

    private static ApplicationDbContext CreateContext(IServiceScope scope, IDomainEventsDispatcher dispatcher) =>
        new(scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>(), dispatcher);

    private static Department AddDepartment(ApplicationDbContext context, string? name = null)
    {
        var department = new Department
        {
            Id = Guid.NewGuid(),
            Name = name ?? $"Departamento {Guid.NewGuid():N}",
            Description = "Criado pelo teste de publicação de eventos.",
            IsActive = true
        };

        department.Raise(new DepartmentCreatedDomainEvent(department.Id));

        context.Departments.Add(department);

        return department;
    }

    /// <summary>
    /// Guarda os eventos publicados, no lugar do dispatcher real, para o teste conferir o que saiu do contexto.
    /// </summary>
    private sealed class RecordingDomainEventsDispatcher : IDomainEventsDispatcher
    {
        public List<IDomainEvent> Published { get; } = [];

        public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            Published.AddRange(domainEvents);

            return Task.CompletedTask;
        }
    }
}
