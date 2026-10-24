using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// As roles Atendente e Colaborador saíram do HasData (passaram para o DefaultRolesSeeder). O EF gerou DeleteData
    /// para elas e para as permissões delas; os comandos foram retirados à mão para que bancos existentes mantenham
    /// essas roles, as permissões editadas pelos administradores e os usuários vinculados.
    /// </remarks>
    public partial class Role_Seed_Only_Administrator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_roles_is_administrator",
                schema: "public",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "is_administrator",
                schema: "public",
                table: "roles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_administrator",
                schema: "public",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("0199a7c4-0000-7000-8000-000000000001"),
                column: "is_administrator",
                value: true);

            migrationBuilder.CreateIndex(
                name: "ix_roles_is_administrator",
                schema: "public",
                table: "roles",
                column: "is_administrator",
                unique: true,
                filter: "is_administrator");
        }
    }
}
