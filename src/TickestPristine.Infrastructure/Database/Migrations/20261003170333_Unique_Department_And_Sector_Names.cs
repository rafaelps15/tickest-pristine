using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Unique_Department_And_Sector_Names : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sectors_department_id",
                schema: "public",
                table: "sectors");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "public",
                table: "sectors",
                type: "citext",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "public",
                table: "departments",
                type: "citext",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            // Setor não existe sem departamento ativo: desativa os que ficaram órfãos antes desta regra.
            migrationBuilder.Sql("""
                UPDATE public.sectors s
                SET is_active = false
                FROM public.departments d
                WHERE d.id = s.department_id AND s.is_active AND NOT d.is_active;
                """);

            // Bancos existentes podem ter nomes repetidos entre os ativos; numera as repetições para os índices únicos.
            migrationBuilder.Sql("""
                UPDATE public.departments d
                SET name = d.name || ' (' || r.position || ')'
                FROM (
                    SELECT id, row_number() OVER (PARTITION BY name ORDER BY id) AS position
                    FROM public.departments
                    WHERE is_active
                ) r
                WHERE d.id = r.id AND r.position > 1;
                """);

            migrationBuilder.Sql("""
                UPDATE public.sectors s
                SET name = s.name || ' (' || r.position || ')'
                FROM (
                    SELECT id, row_number() OVER (PARTITION BY department_id, name ORDER BY id) AS position
                    FROM public.sectors
                    WHERE is_active
                ) r
                WHERE s.id = r.id AND r.position > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_sectors_department_id_name",
                schema: "public",
                table: "sectors",
                columns: new[] { "department_id", "name" },
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_departments_name",
                schema: "public",
                table: "departments",
                column: "name",
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sectors_department_id_name",
                schema: "public",
                table: "sectors");

            migrationBuilder.DropIndex(
                name: "ix_departments_name",
                schema: "public",
                table: "departments");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "public",
                table: "sectors",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                schema: "public",
                table: "departments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "ix_sectors_department_id",
                schema: "public",
                table: "sectors",
                column: "department_id");
        }
    }
}
