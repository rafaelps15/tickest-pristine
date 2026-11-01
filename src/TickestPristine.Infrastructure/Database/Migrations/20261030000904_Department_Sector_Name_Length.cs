using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Department_Sector_Name_Length : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_sectors_name_length",
                schema: "public",
                table: "sectors",
                sql: "char_length(name) <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_departments_name_length",
                schema: "public",
                table: "departments",
                sql: "char_length(name) <= 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_sectors_name_length",
                schema: "public",
                table: "sectors");

            migrationBuilder.DropCheckConstraint(
                name: "ck_departments_name_length",
                schema: "public",
                table: "departments");
        }
    }
}
