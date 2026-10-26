using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <summary>
    /// Violação materializada e fila do monitoramento do SLA: <c>breached_on_utc</c>, <c>next_action_on_utc</c> e o índice
    /// filtrado. Os cronômetros existentes são preenchidos aqui; o preenchimento pode rodar de novo sem mudar nada e não
    /// grava configuração.
    /// </summary>
    public partial class Add_Sla_Breach_Monitoring : Migration
    {
        /// <summary>
        /// Cronômetros em aberto (sem conclusão e sem parada) de chamado não excluído entram na fila no próprio prazo; o
        /// monitoramento grava a violação dos vencidos na primeira execução, com evento.
        /// </summary>
        public const string BackfillNextActionSql =
            """
            UPDATE public.ticket_sla_timers s SET next_action_on_utc = s.due_on_utc
            FROM public.tickets t
            WHERE t.id = s.ticket_id AND t.deleted_at_utc IS NULL
              AND s.completed_on_utc IS NULL AND s.stopped_on_utc IS NULL
              AND s.breached_on_utc IS NULL AND s.next_action_on_utc IS NULL;
            """;

        /// <summary>
        /// Cronômetros concluídos depois do prazo ganham a violação no próprio prazo, sem evento.
        /// </summary>
        public const string BackfillBreachedSql =
            """
            UPDATE public.ticket_sla_timers SET breached_on_utc = due_on_utc
            WHERE completed_on_utc > due_on_utc AND breached_on_utc IS NULL;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "breached_on_utc",
                schema: "public",
                table: "ticket_sla_timers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_action_on_utc",
                schema: "public",
                table: "ticket_sla_timers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_sla_timers_next_action_on_utc",
                schema: "public",
                table: "ticket_sla_timers",
                column: "next_action_on_utc",
                filter: "next_action_on_utc IS NOT NULL");

            migrationBuilder.Sql(BackfillNextActionSql);

            migrationBuilder.Sql(BackfillBreachedSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ticket_sla_timers_next_action_on_utc",
                schema: "public",
                table: "ticket_sla_timers");

            migrationBuilder.DropColumn(
                name: "breached_on_utc",
                schema: "public",
                table: "ticket_sla_timers");

            migrationBuilder.DropColumn(
                name: "next_action_on_utc",
                schema: "public",
                table: "ticket_sla_timers");
        }
    }
}
