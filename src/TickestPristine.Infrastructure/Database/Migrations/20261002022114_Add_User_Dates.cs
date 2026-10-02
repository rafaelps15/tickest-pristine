using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_User_Dates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "created_at_utc",
                schema: "public",
                table: "users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            // O padrão só serve para preencher os usuários já existentes; daqui em diante a aplicação grava a data.
            migrationBuilder.Sql("ALTER TABLE public.users ALTER COLUMN created_at_utc DROP DEFAULT;");

            migrationBuilder.AddColumn<DateTime>(
                name: "deactivated_at_utc",
                schema: "public",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_at_utc",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "deactivated_at_utc",
                schema: "public",
                table: "users");
        }
    }
}
