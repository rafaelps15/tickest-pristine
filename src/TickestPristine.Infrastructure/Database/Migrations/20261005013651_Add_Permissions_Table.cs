using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_Permissions_Table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_roles_is_default",
                schema: "public",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "is_default",
                schema: "public",
                table: "roles");

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "public",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    group = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_administrative = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.code);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "permissions",
                columns: new[] { "code", "description", "group", "is_administrative", "name" },
                values: new object[,]
                {
                    { "audit:read", "Consultar quem alterou cada registro, quando e quais valores mudaram.", "Auditoria", true, "Ver o log de auditoria" },
                    { "departments:manage", "Criar, editar e desativar departamentos.", "Estrutura organizacional", true, "Gerenciar departamentos" },
                    { "roles:manage", "Criar funções e definir as permissões de cada uma.", "Funções e permissões", true, "Gerenciar funções" },
                    { "roles:read", "Consultar as funções existentes e as permissões de cada uma.", "Funções e permissões", true, "Ver funções" },
                    { "sectors:manage", "Criar, editar e desativar setores.", "Estrutura organizacional", true, "Gerenciar setores" },
                    { "tickets:create", "Abrir novos chamados.", "Chamados", false, "Abrir chamados" },
                    { "tickets:delete-own", "Excluir os chamados que a própria pessoa abriu.", "Chamados", false, "Excluir os próprios chamados" },
                    { "tickets:manage", "Ver e tratar os chamados de todos os colaboradores.", "Chamados", false, "Atender a fila de chamados" },
                    { "tickets:reopen-own", "Reabrir chamados resolvidos que a própria pessoa abriu.", "Chamados", false, "Reabrir os próprios chamados" },
                    { "tickets:update-own", "Alterar os chamados que a própria pessoa abriu.", "Chamados", false, "Editar os próprios chamados" },
                    { "tickets:view-own", "Consultar os chamados que a própria pessoa abriu.", "Chamados", false, "Ver os próprios chamados" },
                    { "users:assign-roles", "Definir quais funções cada colaborador tem.", "Usuários", true, "Alterar funções de colaboradores" },
                    { "users:create", "Incluir novos colaboradores no sistema.", "Usuários", true, "Cadastrar colaboradores" },
                    { "users:deactivate", "Bloquear ou devolver o acesso de colaboradores ao sistema.", "Usuários", true, "Desativar e reativar colaboradores" },
                    { "users:read", "Listar e consultar os colaboradores cadastrados.", "Usuários", true, "Ver usuários" },
                    { "users:update", "Alterar nome e sobrenome de outros colaboradores.", "Usuários", true, "Editar dados de colaboradores" }
                });

            // Atendente, Colaborador e as permissões deles já existem em todo banco (vieram da migration Initial) e podem
            // ter sido editados pelos administradores: o InsertData gerado foi retirado para não duplicar nem desfazer
            // essas edições. Vínculos com códigos fora do catálogo são descartados antes da chave estrangeira.
            migrationBuilder.Sql(
                "DELETE FROM public.role_permissions WHERE permission_code NOT IN (SELECT code FROM public.permissions);");

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_code",
                schema: "public",
                table: "role_permissions",
                column: "permission_code");

            migrationBuilder.AddForeignKey(
                name: "fk_role_permissions_permissions_permission_code",
                schema: "public",
                table: "role_permissions",
                column: "permission_code",
                principalSchema: "public",
                principalTable: "permissions",
                principalColumn: "code",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_role_permissions_permissions_permission_code",
                schema: "public",
                table: "role_permissions");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_role_permissions_permission_code",
                schema: "public",
                table: "role_permissions");

            migrationBuilder.AddColumn<bool>(
                name: "is_default",
                schema: "public",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("0199a7c4-0000-7000-8000-000000000003"),
                column: "is_default",
                value: true);

            migrationBuilder.CreateIndex(
                name: "ix_roles_is_default",
                schema: "public",
                table: "roles",
                column: "is_default",
                unique: true,
                filter: "is_default");
        }
    }
}
