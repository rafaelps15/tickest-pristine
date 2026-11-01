using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_Ticket_User_Text_Length_Constraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_users_email_length",
                schema: "public",
                table: "users",
                sql: "char_length(email) <= 254");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_first_name_length",
                schema: "public",
                table: "users",
                sql: "char_length(first_name) <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_last_name_length",
                schema: "public",
                table: "users",
                sql: "char_length(last_name) <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tickets_description_length",
                schema: "public",
                table: "tickets",
                sql: "char_length(description) BETWEEN 10 AND 500");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tickets_title_length",
                schema: "public",
                table: "tickets",
                sql: "char_length(title) <= 200");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_email_length",
                schema: "public",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_first_name_length",
                schema: "public",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_last_name_length",
                schema: "public",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tickets_description_length",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tickets_title_length",
                schema: "public",
                table: "tickets");
        }
    }
}
