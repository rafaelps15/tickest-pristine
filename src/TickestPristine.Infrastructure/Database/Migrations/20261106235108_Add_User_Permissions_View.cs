using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_User_Permissions_View : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Permissões efetivas: a união das permissões das funções de cada usuário, uma definição só para
            // o provedor de permissões e as listas de atendentes. A situação do usuário fica com quem lê.
            migrationBuilder.Sql(
                """
                CREATE VIEW public.user_permissions AS
                SELECT DISTINCT ur.user_id, rp.permission_code
                FROM public.user_roles ur
                JOIN public.role_permissions rp ON rp.role_name = ur.role_name;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW public.user_permissions;");
        }
    }
}
