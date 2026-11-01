using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TickestPristine.Domain.Departments;
using TickestPristine.Infrastructure.Database;
using Npgsql;
using TickestPristine.Infrastructure.DomainEvents;
using TickestPristine.SharedKernel;

namespace TickestPristine.IntegrationTests.Database;

/// <summary>
/// Valida a publicação dos eventos de domínio pelo <see cref="PublishDomainEventsInterceptor"/> nos caminhos assíncronos de
/// gravação, contra o PostgreSQL de verdade: os eventos só saem das entidades depois de um commit bem-sucedido.
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

    // Contexto só com o interceptor de eventos, que resolve o dispatcher que grava o que foi publicado.
    private static ApplicationDbContext CreateContext(IServiceScope scope, IDomainEventsDispatcher dispatcher)
    {
        ServiceProvider dispatcherProvider = new ServiceCollection()
            .AddSingleton(dispatcher)
            .BuildServiceProvider();

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new PublishDomainEventsInterceptor(dispatcherProvider.GetRequiredService<IServiceScopeFactory>()))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Department AddDepartment(ApplicationDbContext context, string? name = null)
    {
        var department = Department.Create(name ?? $"Departamento {Guid.NewGuid():N}", "Criado pelo teste de publicação de eventos.", responsibleUser: null);

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
