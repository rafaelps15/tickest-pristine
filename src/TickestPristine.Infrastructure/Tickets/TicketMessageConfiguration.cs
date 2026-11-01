using TickestPristine.Domain.Tickets;
using TickestPristine.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Tickets;

internal sealed class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Content).HasMaxLength(4000);

        builder.HasQueryFilter(m => m.DeletedAtUtc == null);

        // Paginação por cursor composto (data + id), da mais nova para a mais antiga, dentro de cada chamado.
        builder.HasIndex(m => new { m.TicketId, m.CreatedAtUtc, m.Id })
            .IsDescending(false, true, true);

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(m => m.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
