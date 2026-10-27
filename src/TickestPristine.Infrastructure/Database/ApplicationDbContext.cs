using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Audit;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.SlaCalendars;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using TickestPristine.Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using TickestPristine.SharedKernel;

namespace TickestPristine.Infrastructure.Database;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDomainEventsDispatcher domainEventsDispatcher)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users { get; set; }

    public DbSet<UserCredential> UserCredentials { get; set; }

    public DbSet<UserInvitation> UserInvitations { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<Department> Departments { get; set; }

    public DbSet<Sector> Sectors { get; set; }

    public DbSet<Ticket> Tickets { get; set; }

    public DbSet<TicketAttachment> TicketAttachments { get; set; }

    public DbSet<TicketMessage> TicketMessages { get; set; }

    public DbSet<TicketHistory> TicketHistories { get; set; }

    public DbSet<TicketSlaTimer> TicketSlaTimers { get; set; }

    public DbSet<Role> Roles { get; set; }

    public DbSet<Permission> Permissions { get; set; }

    public DbSet<RolePermission> RolePermissions { get; set; }

    public DbSet<UserRole> UserRoles { get; set; }

    public DbSet<AuditLogEntry> AuditLogs { get; set; }

    public DbSet<SlaCalendar> SlaCalendars { get; set; }

    public DbSet<SlaCalendarHoliday> SlaCalendarHolidays { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Texto que compara sem diferenciar maiúsculas de minúsculas (usado em nome e e-mail do usuário).
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.HasDefaultSchema(Schemas.Default);
    }

    // As sobrecargas com acceptAllChangesOnSuccess são as que as demais chamam: sobrescrevê-las garante que nenhum
    // caminho de gravação (síncrono, assíncrono, com ou sem acceptAllChanges) deixe de publicar os eventos.
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        // Salva primeiro e depois publica os eventos de domínio; os handlers dos eventos rodam fora desta transação.
        // As entidades são guardadas antes do save, porque as removidas saem do ChangeTracker com o commit.
        List<Entity> entitiesWithEvents = GetEntitiesWithDomainEvents();

        int result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        await PublishDomainEventsAsync(ExtractDomainEvents(entitiesWithEvents));

        return result;
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        List<Entity> entitiesWithEvents = GetEntitiesWithDomainEvents();

        int result = base.SaveChanges(acceptAllChangesOnSuccess);

        // Os handlers são assíncronos; o caminho síncrono existe para não perder eventos e não é usado nas requisições.
        PublishDomainEventsAsync(ExtractDomainEvents(entitiesWithEvents)).GetAwaiter().GetResult();

        return result;
    }

    private Task PublishDomainEventsAsync(List<IDomainEvent> domainEvents) =>
        // Como a invalidação de cache depois do commit, os handlers não são cancelados junto com a requisição.
        domainEventsDispatcher.DispatchAsync(domainEvents, CancellationToken.None);

    private List<Entity> GetEntitiesWithDomainEvents() =>
        ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Any())
            .ToList();

    /// <summary>
    /// Lê e limpa os eventos das entidades. Chamado só depois do commit: se o save falhar, os eventos continuam nas
    /// entidades para uma nova tentativa.
    /// </summary>
    private static List<IDomainEvent> ExtractDomainEvents(List<Entity> entities)
    {
        List<IDomainEvent> domainEvents = [.. entities.SelectMany(entity => entity.DomainEvents)];

        entities.ForEach(entity => entity.ClearDomainEvents());

        return domainEvents;
    }
}
