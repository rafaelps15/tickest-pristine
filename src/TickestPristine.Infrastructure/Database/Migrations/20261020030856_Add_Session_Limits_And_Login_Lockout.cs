using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_Session_Limits_And_Login_Lockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_login_attempts",
                schema: "public",
                table: "user_credentials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "lockout_ends_at_utc",
                schema: "public",
                table: "user_credentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "security_stamp",
                schema: "public",
                table: "user_credentials",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<string>(
                name: "previous_token_hash",
                schema: "public",
                table: "refresh_tokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rotated_at_utc",
                schema: "public",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "security_stamp",
                schema: "public",
                table: "refresh_tokens",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<DateTime>(
                name: "session_expires_on_utc",
                schema: "public",
                table: "refresh_tokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // As sessões já abertas continuam valendo: o limite delas é a validade atual, e o carimbo, o da credencial.
            migrationBuilder.Sql(
                """
                UPDATE public.refresh_tokens AS rt
                SET session_expires_on_utc = rt.expires_on_utc,
                    security_stamp = uc.security_stamp
                FROM public.user_credentials AS uc
                WHERE uc.user_id = rt.user_id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_previous_token_hash",
                schema: "public",
                table: "refresh_tokens",
                column: "previous_token_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_previous_token_hash",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "failed_login_attempts",
                schema: "public",
                table: "user_credentials");

            migrationBuilder.DropColumn(
                name: "lockout_ends_at_utc",
                schema: "public",
                table: "user_credentials");

            migrationBuilder.DropColumn(
                name: "security_stamp",
                schema: "public",
                table: "user_credentials");

            migrationBuilder.DropColumn(
                name: "previous_token_hash",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "rotated_at_utc",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "security_stamp",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "session_expires_on_utc",
                schema: "public",
                table: "refresh_tokens");
        }
    }
}
