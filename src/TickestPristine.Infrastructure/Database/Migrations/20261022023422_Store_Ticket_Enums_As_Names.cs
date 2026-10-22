using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <summary>
    /// Status e prioridade do chamado e a ação do histórico passam a ser gravados pelo nome do enum (ex.: "InProgress"),
    /// legíveis no banco. O ALTER COLUMN gerado pelo EF converteria 2 em '2'; o USING traduz cada número para o nome.
    /// </summary>
    public partial class Store_Ticket_Enums_As_Names : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE public.tickets
                    ALTER COLUMN status TYPE character varying(20) USING CASE status
                        WHEN 1 THEN 'Open'
                        WHEN 2 THEN 'InProgress'
                        WHEN 3 THEN 'Resolved'
                        WHEN 4 THEN 'Closed'
                        WHEN 5 THEN 'Canceled'
                    END,
                    ALTER COLUMN priority TYPE character varying(20) USING CASE priority
                        WHEN 1 THEN 'Low'
                        WHEN 2 THEN 'Medium'
                        WHEN 3 THEN 'High'
                        WHEN 4 THEN 'Urgent'
                    END;

                ALTER TABLE public.ticket_histories
                    ALTER COLUMN action TYPE character varying(30) USING CASE action
                        WHEN 1 THEN 'Created'
                        WHEN 2 THEN 'StatusChanged'
                        WHEN 3 THEN 'Reassigned'
                        WHEN 4 THEN 'Reopened'
                        WHEN 5 THEN 'Deleted'
                        WHEN 6 THEN 'MessageAdded'
                        WHEN 7 THEN 'AttachmentAdded'
                        WHEN 8 THEN 'AttachmentRemoved'
                        WHEN 9 THEN 'MessageEdited'
                        WHEN 10 THEN 'MessageRemoved'
                    END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE public.tickets
                    ALTER COLUMN status TYPE integer USING CASE status
                        WHEN 'Open' THEN 1
                        WHEN 'InProgress' THEN 2
                        WHEN 'Resolved' THEN 3
                        WHEN 'Closed' THEN 4
                        WHEN 'Canceled' THEN 5
                    END,
                    ALTER COLUMN priority TYPE integer USING CASE priority
                        WHEN 'Low' THEN 1
                        WHEN 'Medium' THEN 2
                        WHEN 'High' THEN 3
                        WHEN 'Urgent' THEN 4
                    END;

                ALTER TABLE public.ticket_histories
                    ALTER COLUMN action TYPE integer USING CASE action
                        WHEN 'Created' THEN 1
                        WHEN 'StatusChanged' THEN 2
                        WHEN 'Reassigned' THEN 3
                        WHEN 'Reopened' THEN 4
                        WHEN 'Deleted' THEN 5
                        WHEN 'MessageAdded' THEN 6
                        WHEN 'AttachmentAdded' THEN 7
                        WHEN 'AttachmentRemoved' THEN 8
                        WHEN 'MessageEdited' THEN 9
                        WHEN 'MessageRemoved' THEN 10
                    END;
                """);
        }
    }
}
