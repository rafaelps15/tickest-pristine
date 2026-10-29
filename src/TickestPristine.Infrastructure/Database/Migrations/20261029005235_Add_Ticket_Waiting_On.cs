using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_Ticket_Waiting_On : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "waiting_on",
                schema: "public",
                table: "tickets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Chamados existentes (spec 029, REQ-3), pela mesma regra do TicketWaitingInterceptor: inativo fica sem
            // situação; ativo aguarda o solicitante quando a última mensagem válida é de quem atende (não é o solicitante,
            // está ativo e tem tickets:manage em alguma role, pela permissão de hoje, DEC-4) e quem atende nos demais casos.
            migrationBuilder.Sql(
                """
                UPDATE public.tickets AS t
                SET waiting_on = CASE
                    WHEN t.status NOT IN ('Open', 'InProgress') THEN NULL
                    WHEN COALESCE((
                        SELECT m.author_user_id <> t.created_by_user_id AND EXISTS (
                            SELECT 1
                            FROM public.users AS u
                            JOIN public.user_roles AS ur ON ur.user_id = u.id
                            JOIN public.role_permissions AS rp ON rp.role_name = ur.role_name
                            WHERE u.id = m.author_user_id
                              AND u.is_active
                              AND rp.permission_code = 'tickets:manage')
                        FROM public.ticket_messages AS m
                        WHERE m.ticket_id = t.id AND m.deleted_at_utc IS NULL
                        ORDER BY m.created_at_utc DESC, m.id DESC
                        LIMIT 1), false) THEN 'Requester'
                    ELSE 'Agent'
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_waiting_on",
                schema: "public",
                table: "tickets",
                column: "waiting_on",
                filter: "waiting_on IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_waiting_on",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "waiting_on",
                schema: "public",
                table: "tickets");
        }
    }
}
