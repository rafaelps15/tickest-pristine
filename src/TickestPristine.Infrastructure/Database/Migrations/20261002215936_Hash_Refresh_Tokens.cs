using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Hash_Refresh_Tokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Os tokens gravados em texto puro não batem com o hash e ficariam todos com token_hash vazio,
            // quebrando o índice único. Apagá-los só exige um novo login.
            migrationBuilder.Sql("DELETE FROM public.refresh_tokens;");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_token",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "token",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.AddColumn<string>(
                name: "token_hash",
                schema: "public",
                table: "refresh_tokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "public",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O valor original dos tokens não pode ser recuperado a partir do hash.
            migrationBuilder.Sql("DELETE FROM public.refresh_tokens;");

            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "token_hash",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.AddColumn<string>(
                name: "token",
                schema: "public",
                table: "refresh_tokens",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token",
                schema: "public",
                table: "refresh_tokens",
                column: "token",
                unique: true);
        }
    }
}
