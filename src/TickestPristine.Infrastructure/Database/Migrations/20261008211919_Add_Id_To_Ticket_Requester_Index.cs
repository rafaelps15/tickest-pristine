using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_Id_To_Ticket_Requester_Index : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_created_by_user_id_created_at_utc",
                schema: "public",
                table: "tickets");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_created_by_user_id_created_at_utc_id",
                schema: "public",
                table: "tickets",
                columns: new[] { "created_by_user_id", "created_at_utc", "id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_created_by_user_id_created_at_utc_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_created_by_user_id_created_at_utc",
                schema: "public",
                table: "tickets",
                columns: new[] { "created_by_user_id", "created_at_utc" },
                descending: new[] { false, true });
        }
    }
}
