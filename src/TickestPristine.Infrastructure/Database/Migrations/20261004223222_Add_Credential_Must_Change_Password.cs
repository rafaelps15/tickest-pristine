using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_Credential_Must_Change_Password : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                schema: "public",
                table: "user_credentials",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "must_change_password",
                schema: "public",
                table: "user_credentials");
        }
    }
}
