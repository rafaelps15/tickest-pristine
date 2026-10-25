using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <summary>
    /// Plano de SLA no chamado e cronômetros de primeira resposta e resolução. Os chamados existentes ganham o plano
    /// "Legado (horas corridas)", no calendário "24 horas" em código, e o cronômetro de resolução do ciclo 1 com o
    /// prazo e o alerta que já tinham; não ganham primeira resposta. O preenchimento pode rodar de novo sem duplicar
    /// nada e não grava política nem calendário no banco.
    /// </summary>
    public partial class Add_Ticket_Sla_Timers : Migration
    {
        /// <summary>
        /// Plano legado para os chamados sem plano, com a meta igual às horas corridas de hoje para a prioridade. Os
        /// nomes das propriedades são os do <c>TicketSlaPlan</c>, como o EF grava.
        /// </summary>
        public const string BackfillSlaPlansSql =
            """
            UPDATE public.tickets
            SET sla_plan = jsonb_build_object(
                'PolicyId', NULL,
                'PolicyName', 'Legado (horas corridas)',
                'PolicyVersion', 1,
                'CalendarId', '00000000-0000-0000-0000-000000000002',
                'FirstResponseMinutes', NULL,
                'ResolutionMinutes', CASE priority
                    WHEN 'Urgent' THEN 240
                    WHEN 'High' THEN 480
                    WHEN 'Medium' THEN 1440
                    ELSE 4320
                END,
                'IsLegacy', true)
            WHERE sla_plan IS NULL;
            """;

        /// <summary>
        /// Cronômetro de resolução do ciclo 1 dos chamados legados, com o início, o prazo e o alerta já gravados no
        /// chamado: concluído na primeira resolução; parado, se cancelado sem resolução, no instante do cancelamento
        /// registrado no histórico (ou na abertura, se o histórico não o tiver).
        /// </summary>
        public const string BackfillResolutionTimersSql =
            """
            INSERT INTO public.ticket_sla_timers (
                id, ticket_id, metric, cycle, target_minutes, calendar_id,
                started_at_utc, warning_on_utc, due_on_utc, completed_on_utc, stopped_on_utc)
            SELECT
                gen_random_uuid(),
                t.id,
                'Resolution',
                1,
                (t.sla_plan ->> 'ResolutionMinutes')::integer,
                (t.sla_plan ->> 'CalendarId')::uuid,
                t.created_at_utc,
                t.sla_warning_at_utc,
                t.due_at_utc,
                t.resolved_at_utc,
                CASE
                    WHEN t.resolved_at_utc IS NULL AND t.status = 'Canceled' THEN COALESCE(
                        (SELECT max(h.occurred_at_utc)
                         FROM public.ticket_histories h
                         WHERE h.ticket_id = t.id AND h.action = 'StatusChanged' AND h.new_value = 'Canceled'),
                        t.created_at_utc)
                END
            FROM public.tickets t
            WHERE (t.sla_plan ->> 'IsLegacy')::boolean
            ON CONFLICT (ticket_id, metric, cycle) DO NOTHING;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nula até o preenchimento; obrigatória logo depois, no fim desta migration.
            migrationBuilder.AddColumn<string>(
                name: "sla_plan",
                schema: "public",
                table: "tickets",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ticket_sla_timers",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cycle = table.Column<int>(type: "integer", nullable: false),
                    target_minutes = table.Column<int>(type: "integer", nullable: false),
                    calendar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    warning_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    due_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    stopped_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_sla_timers", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_sla_timers_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_sla_timers_ticket_id_metric_cycle",
                schema: "public",
                table: "ticket_sla_timers",
                columns: new[] { "ticket_id", "metric", "cycle" },
                unique: true);

            migrationBuilder.Sql(BackfillSlaPlansSql);

            migrationBuilder.Sql(BackfillResolutionTimersSql);

            migrationBuilder.AlterColumn<string>(
                name: "sla_plan",
                schema: "public",
                table: "tickets",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ticket_sla_timers",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "sla_plan",
                schema: "public",
                table: "tickets");
        }
    }
}
