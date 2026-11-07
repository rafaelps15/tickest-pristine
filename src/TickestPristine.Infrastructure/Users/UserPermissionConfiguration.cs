using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TickestPristine.Infrastructure.Users;

internal sealed class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.HasNoKey();

        // A view é criada pela migration e não olha a situação do usuário: quem lê aplica o filtro de ativo.
        builder.ToView("user_permissions");
    }
}
