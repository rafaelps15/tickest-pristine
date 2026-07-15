using TickestPristine.Application.Abstractions.Data;
using TickestPristine.Domain.Departments;
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

    public DbSet<UserPermission> UserPermissions { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<Department> Departments { get; set; }

    public DbSet<Sector> Sectors { get; set; }

    public DbSet<Ticket> Tickets { get; set; }
}
