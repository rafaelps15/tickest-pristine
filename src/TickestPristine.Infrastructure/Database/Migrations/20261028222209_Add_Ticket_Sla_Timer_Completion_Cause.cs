using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_Ticket_Sla_Timer_Completion_Cause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "completion_cause",
                schema: "public",
                table: "ticket_sla_timers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Cronômetros já concluídos (spec 027, REQ-2): a primeira resposta é "Message" quando há mensagem de quem não
            // é o solicitante no mesmo instante da conclusão (a mensagem e a conclusão gravam o mesmo horário), e
            // "Resolution" nos demais casos; a resolução é sempre "Resolution". Inclui mensagens excluídas depois, que
            // existiam quando concluíram o cronômetro.
            migrationBuilder.Sql(
                """
                UPDATE public.ticket_sla_timers AS s
                SET completion_cause = CASE
                    WHEN s.metric = 'FirstResponse' AND EXISTS (
                        SELECT 1
                        FROM public.ticket_messages AS m
                        JOIN public.tickets AS t ON t.id = s.ticket_id
                        WHERE m.ticket_id = s.ticket_id
                          AND m.author_user_id <> t.created_by_user_id
                          AND m.created_at_utc = s.completed_on_utc)
                    THEN 'Message'
                    ELSE 'Resolution'
                END
                WHERE s.completed_on_utc IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "completion_cause",
                schema: "public",
                table: "ticket_sla_timers");
        }
    }
}
