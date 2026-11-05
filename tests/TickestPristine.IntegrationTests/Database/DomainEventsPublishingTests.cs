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

    [Fact]
    public async Task SaveChangesAsync_Should_KeepEventsOfRemovedEntityAndPublishNothing_When_SaveFails()
    {
        // Arrange: a remoção falha porque o id não existe no banco
        var dispatcher = new RecordingDomainEventsDispatcher();
        using IServiceScope scope = Services.CreateScope();
        await using EventProbeDbContext context = await CreateProbeContextAsync(scope, dispatcher);
        var probe = EventProbe.Create();
        context.Probes.Attach(probe);
        probe.Discard();
        context.Probes.Remove(probe);

        // Act
        Func<Task> save = () => context.SaveChangesAsync();

        // Assert
        await save.ShouldThrowAsync<DbUpdateConcurrencyException>();
        dispatcher.Published.ShouldBeEmpty();
        probe.DomainEvents.ShouldHaveSingleItem().ShouldBe(new EventProbeDiscardedDomainEvent(probe.Id));
    }

    // Contexto da entidade de teste, numa tabela própria do banco dos testes, com o mesmo interceptor de eventos.
    private static async Task<EventProbeDbContext> CreateProbeContextAsync(IServiceScope scope, IDomainEventsDispatcher dispatcher)
    {
        ServiceProvider dispatcherProvider = new ServiceCollection()
            .AddSingleton(dispatcher)
            .BuildServiceProvider();

        DbContextOptions<EventProbeDbContext> options = new DbContextOptionsBuilder<EventProbeDbContext>()
            .UseNpgsql(scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>())
            .AddInterceptors(new PublishDomainEventsInterceptor(dispatcherProvider.GetRequiredService<IServiceScopeFactory>()))
            .Options;

        var context = new EventProbeDbContext(options);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE SCHEMA IF NOT EXISTS integration_tests; " +
            "CREATE TABLE IF NOT EXISTS integration_tests.event_probes (id uuid PRIMARY KEY);");

        return context;
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

// Entidade e evento só destes testes: a remoção dispara o evento, como faria uma entidade do domínio.
internal sealed class EventProbe : Entity
{
    private EventProbe()
    {
    }

    public Guid Id { get; private set; }

    public static EventProbe Create() => new() { Id = Guid.NewGuid() };

    public void Discard() => Raise(new EventProbeDiscardedDomainEvent(Id));
}

internal sealed record EventProbeDiscardedDomainEvent(Guid ProbeId) : IDomainEvent;

internal sealed class EventProbeDbContext(DbContextOptions<EventProbeDbContext> options) : DbContext(options)
{
    public DbSet<EventProbe> Probes => Set<EventProbe>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventProbe>(probe =>
        {
            probe.ToTable("event_probes", "integration_tests");
            probe.HasKey(p => p.Id);
            probe.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
            probe.Ignore(p => p.DomainEvents);
        });
    }
}
