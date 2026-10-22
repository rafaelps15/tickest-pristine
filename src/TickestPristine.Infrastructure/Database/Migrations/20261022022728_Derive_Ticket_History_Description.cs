using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <summary>
    /// O histórico deixa de gravar a frase pronta: o texto passa a ser montado na leitura a partir da ação e dos valores
    /// (TicketHistoryDescriptions). O nome do anexo, que só existia dentro da frase, vai antes para new_value.
    /// </summary>
    public partial class Derive_Ticket_History_Description : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 7 = TicketHistoryAction.AttachmentAdded; a frase era "Anexo '<nome>' adicionado".
            migrationBuilder.Sql(
                """
                UPDATE public.ticket_histories
                SET new_value = substring(description FROM '^Anexo ''(.*)'' adicionado$')
                WHERE action = 7 AND new_value IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "description",
                schema: "public",
                table: "ticket_histories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "public",
                table: "ticket_histories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // Remonta as frases como o TicketHistoryDescriptions as monta hoje.
            migrationBuilder.Sql(
                """
                UPDATE public.ticket_histories h
                SET description = CASE h.action
                    WHEN 1 THEN 'Chamado criado'
                    WHEN 2 THEN 'Status alterado de ' || s_old.name || ' para ' || s_new.name
                    WHEN 3 THEN 'Responsável alterado'
                    WHEN 4 THEN 'Chamado reaberto'
                    WHEN 5 THEN 'Chamado excluído'
                    WHEN 6 THEN 'Nova mensagem adicionada ao chamado'
                    WHEN 7 THEN 'Anexo ''' || COALESCE(h.new_value, '') || ''' adicionado'
                    WHEN 8 THEN 'Anexo removido'
                    WHEN 9 THEN 'Mensagem editada'
                    WHEN 10 THEN 'Mensagem removida'
                    ELSE h.action::text
                END
                FROM public.ticket_histories source
                CROSS JOIN LATERAL (
                    SELECT CASE source.old_value
                        WHEN 'Open' THEN 'Aberto'
                        WHEN 'InProgress' THEN 'Em andamento'
                        WHEN 'Resolved' THEN 'Resolvido'
                        WHEN 'Closed' THEN 'Fechado'
                        WHEN 'Canceled' THEN 'Cancelado'
                        ELSE COALESCE(source.old_value, '')
                    END AS name) s_old
                CROSS JOIN LATERAL (
                    SELECT CASE source.new_value
                        WHEN 'Open' THEN 'Aberto'
                        WHEN 'InProgress' THEN 'Em andamento'
                        WHEN 'Resolved' THEN 'Resolvido'
                        WHEN 'Closed' THEN 'Fechado'
                        WHEN 'Canceled' THEN 'Cancelado'
                        ELSE COALESCE(source.new_value, '')
                    END AS name) s_new
                WHERE source.id = h.id;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "public",
                table: "ticket_histories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }
    }
}
