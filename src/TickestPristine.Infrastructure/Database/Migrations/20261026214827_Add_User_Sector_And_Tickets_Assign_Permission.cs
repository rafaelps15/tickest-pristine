using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <summary>
    /// Setor opcional do usuário e a permissão administrativa <c>tickets:assign</c>, concedida só à role Administrador.
    /// A permissão e o vínculo entram com ON CONFLICT DO NOTHING: o vínculo pode já ter sido concedido pela API.
    /// </summary>
    public partial class Add_User_Sector_And_Tickets_Assign_Permission : Migration
    {
        /// <summary>
        /// Permissão <c>tickets:assign</c> e o vínculo com a role Administrador, com os ids que o
        /// <c>HasData</c> do modelo gera; pode rodar de novo sem erro.
        /// </summary>
        public const string InsertTicketsAssignPermissionSql =
            """
            INSERT INTO public.permissions (id, code, name, description, "group", is_administrative)
            VALUES ('3a8ecf50-4717-c01a-4e61-03f90e9369da', 'tickets:assign', 'Atribuir chamados',
                    'Definir outra pessoa como responsável por um chamado.', 'Chamados', TRUE)
            ON CONFLICT DO NOTHING;

            INSERT INTO public.role_permissions (id, role_name, permission_code)
            VALUES ('d7074855-9d42-a15a-a220-5868fe60f767', 'Administrador', 'tickets:assign')
            ON CONFLICT DO NOTHING;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "sector_id",
                schema: "public",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(InsertTicketsAssignPermissionSql);

            migrationBuilder.CreateIndex(
                name: "ix_users_sector_id",
                schema: "public",
                table: "users",
                column: "sector_id");

            migrationBuilder.AddForeignKey(
                name: "fk_users_sectors_sector_id",
                schema: "public",
                table: "users",
                column: "sector_id",
                principalSchema: "public",
                principalTable: "sectors",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_sectors_sector_id",
                schema: "public",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_sector_id",
                schema: "public",
                table: "users");

            // Pelo código, e não pelo id: o vínculo pode ter sido concedido a outras roles pela API.
            migrationBuilder.Sql(
                """
                DELETE FROM public.role_permissions WHERE permission_code = 'tickets:assign';
                DELETE FROM public.permissions WHERE code = 'tickets:assign';
                """);

            migrationBuilder.DropColumn(
                name: "sector_id",
                schema: "public",
                table: "users");
        }
    }
}
