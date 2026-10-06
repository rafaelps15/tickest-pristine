using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_Ticket_Sla_And_Pagination_Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_created_by_user_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_ticket_messages_ticket_id",
                schema: "public",
                table: "ticket_messages");

            migrationBuilder.DropIndex(
                name: "ix_ticket_histories_ticket_id",
                schema: "public",
                table: "ticket_histories");

            migrationBuilder.DropIndex(
                name: "ix_ticket_attachments_ticket_id",
                schema: "public",
                table: "ticket_attachments");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_entity_name",
                schema: "public",
                table: "audit_logs");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_entity_name_entity_id",
                schema: "public",
                table: "audit_logs");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                schema: "public",
                table: "tickets",
                type: "citext",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<DateTime>(
                name: "due_at_utc",
                schema: "public",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "resolved_at_utc",
                schema: "public",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "sla_warning_at_utc",
                schema: "public",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Prazos dos chamados existentes pela política da época desta migration (TicketSla): 4, 8, 24 e 72 horas
            // corridas para Urgente (4), Alta (3), Média (2) e Baixa (1); o alerta, em 75% da janela.
            migrationBuilder.Sql(
                """
                UPDATE public.tickets
                SET due_at_utc = created_at_utc + sla.resolution_time,
                    sla_warning_at_utc = created_at_utc + sla.resolution_time * 0.75
                FROM (
                    SELECT t.id,
                           CASE t.priority
                               WHEN 4 THEN interval '4 hours'
                               WHEN 3 THEN interval '8 hours'
                               WHEN 2 THEN interval '24 hours'
                               ELSE interval '72 hours'
                           END AS resolution_time
                    FROM public.tickets t
                ) AS sla
                WHERE tickets.id = sla.id;
                """);

            // Primeira resolução de cada chamado, tirada do histórico (ação StatusChanged = 2 para "Resolved").
            migrationBuilder.Sql(
                """
                UPDATE public.tickets
                SET resolved_at_utc = first_resolution.occurred_at_utc
                FROM (
                    SELECT ticket_id, MIN(occurred_at_utc) AS occurred_at_utc
                    FROM public.ticket_histories
                    WHERE action = 2 AND new_value = 'Resolved'
                    GROUP BY ticket_id
                ) AS first_resolution
                WHERE tickets.id = first_resolution.ticket_id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_users_first_name_last_name_id",
                schema: "public",
                table: "users",
                columns: new[] { "first_name", "last_name", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_created_by_user_id_created_at_utc",
                schema: "public",
                table: "tickets",
                columns: new[] { "created_by_user_id", "created_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_status_due_at_utc",
                schema: "public",
                table: "tickets",
                columns: new[] { "status", "due_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_messages_ticket_id_created_at_utc_id",
                schema: "public",
                table: "ticket_messages",
                columns: new[] { "ticket_id", "created_at_utc", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_histories_ticket_id_occurred_at_utc_id",
                schema: "public",
                table: "ticket_histories",
                columns: new[] { "ticket_id", "occurred_at_utc", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_attachments_ticket_id_uploaded_at_utc_id",
                schema: "public",
                table: "ticket_attachments",
                columns: new[] { "ticket_id", "uploaded_at_utc", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_name_entity_id_timestamp_id",
                schema: "public",
                table: "audit_logs",
                columns: new[] { "entity_name", "entity_id", "timestamp", "id" },
                descending: new[] { false, false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_first_name_last_name_id",
                schema: "public",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_tickets_created_by_user_id_created_at_utc",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_status_due_at_utc",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_ticket_messages_ticket_id_created_at_utc_id",
                schema: "public",
                table: "ticket_messages");

            migrationBuilder.DropIndex(
                name: "ix_ticket_histories_ticket_id_occurred_at_utc_id",
                schema: "public",
                table: "ticket_histories");

            migrationBuilder.DropIndex(
                name: "ix_ticket_attachments_ticket_id_uploaded_at_utc_id",
                schema: "public",
                table: "ticket_attachments");

            migrationBuilder.DropIndex(
                name: "ix_audit_logs_entity_name_entity_id_timestamp_id",
                schema: "public",
                table: "audit_logs");

            migrationBuilder.DropColumn(
                name: "due_at_utc",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "resolved_at_utc",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_warning_at_utc",
                schema: "public",
                table: "tickets");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                schema: "public",
                table: "tickets",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_created_by_user_id",
                schema: "public",
                table: "tickets",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_messages_ticket_id",
                schema: "public",
                table: "ticket_messages",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_histories_ticket_id",
                schema: "public",
                table: "ticket_histories",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_attachments_ticket_id",
                schema: "public",
                table: "ticket_attachments",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_name",
                schema: "public",
                table: "audit_logs",
                column: "entity_name");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_name_entity_id",
                schema: "public",
                table: "audit_logs",
                columns: new[] { "entity_name", "entity_id" });
        }
    }
}
