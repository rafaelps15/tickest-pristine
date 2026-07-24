using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Departments;
using TickestPristine.Domain.Roles;
using TickestPristine.Domain.Sectors;
using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace TickestPristine.Application.UnitTests.Abstractions;

/// <summary>
/// A lightweight in-memory <see cref="DbContext"/> that implements <see cref="IApplicationDbContext"/>
/// so Application handlers can be unit tested without referencing the Infrastructure layer.
/// </summary>
public sealed class TestDbContext(DbContextOptions<TestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users { get; set; }

    public DbSet<UserCredential> UserCredentials { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<Department> Departments { get; set; }

    public DbSet<Sector> Sectors { get; set; }

    public DbSet<Ticket> Tickets { get; set; }

    public DbSet<TicketAttachment> TicketAttachments { get; set; }

    public DbSet<TicketMessage> TicketMessages { get; set; }

    public DbSet<TicketHistory> TicketHistories { get; set; }

    public DbSet<Role> Roles { get; set; }

    public DbSet<RolePermission> RolePermissions { get; set; }

    public DbSet<UserRole> UserRoles { get; set; }

    // Mirrors the soft-delete query filters defined in the real ApplicationDbContext's
    // IEntityTypeConfiguration classes (Infrastructure), which this lightweight double doesn't apply
    // otherwise, so handler tests see the same "deleted rows are invisible" behavior as production.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Ticket>().HasQueryFilter(t => t.DeletedAtUtc == null);
        modelBuilder.Entity<TicketMessage>().HasQueryFilter(m => m.DeletedAtUtc == null);
        modelBuilder.Entity<TicketAttachment>().HasQueryFilter(a => a.DeletedAtUtc == null);
    }
}
