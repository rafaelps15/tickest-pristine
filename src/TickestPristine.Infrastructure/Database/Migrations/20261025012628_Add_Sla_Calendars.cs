using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_Sla_Calendars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sla_calendars",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is24x7 = table.Column<bool>(type: "boolean", nullable: false),
                    work_schedule = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_calendars", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sla_calendar_holidays",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sla_calendar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_calendar_holidays", x => x.id);
                    table.ForeignKey(
                        name: "fk_sla_calendar_holidays_sla_calendars_sla_calendar_id",
                        column: x => x.sla_calendar_id,
                        principalSchema: "public",
                        principalTable: "sla_calendars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sla_calendar_holidays_sla_calendar_id_date",
                schema: "public",
                table: "sla_calendar_holidays",
                columns: new[] { "sla_calendar_id", "date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sla_calendar_holidays",
                schema: "public");

            migrationBuilder.DropTable(
                name: "sla_calendars",
                schema: "public");
        }
    }
}
