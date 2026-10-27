using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_Ticket_Last_Activity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_created_by_user_id_created_at_utc_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_activity_at_utc",
                schema: "public",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Chamados existentes: a ação mais recente do histórico, ou a abertura quando não houver histórico (spec 015,
            // DEC-9). Inclui os excluídos logicamente, que também têm a coluna obrigatória.
            migrationBuilder.Sql(
                """
                UPDATE public.tickets AS t
                SET last_activity_at_utc = COALESCE(
                    (SELECT MAX(h.occurred_at_utc) FROM public.ticket_histories AS h WHERE h.ticket_id = t.id),
                    t.created_at_utc);
                """);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_created_by_user_id_last_activity_at_utc_id",
                schema: "public",
                table: "tickets",
                columns: new[] { "created_by_user_id", "last_activity_at_utc", "id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_created_by_user_id_last_activity_at_utc_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "last_activity_at_utc",
                schema: "public",
                table: "tickets");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_created_by_user_id_created_at_utc_id",
                schema: "public",
                table: "tickets",
                columns: new[] { "created_by_user_id", "created_at_utc", "id" },
                descending: new[] { false, true, true });
        }
    }
}
