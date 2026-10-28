using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <summary>
    /// Troca <c>tickets:delete-own</c> pelas permissões administrativas <c>tickets:cancel</c> e <c>tickets:delete</c>,
    /// concedidas só à role Administrador (spec 016). A remoção apaga também os vínculos concedidos pela API; as
    /// inclusões usam ON CONFLICT DO NOTHING, porque um vínculo pode já existir.
    /// </summary>
    public partial class Replace_Ticket_Delete_Own_With_Cancel_And_Delete_Permissions : Migration
    {
        /// <summary>
        /// Remove <c>tickets:delete-own</c> com todos os vínculos dela e grava as duas permissões novas e os vínculos do
        /// Administrador, com os ids que o <c>HasData</c> do modelo gera; pode rodar de novo sem erro.
        /// </summary>
        public const string ReplacePermissionsSql =
            """
            DELETE FROM public.role_permissions WHERE permission_code = 'tickets:delete-own';
            DELETE FROM public.permissions WHERE code = 'tickets:delete-own';

            INSERT INTO public.permissions (id, code, name, description, "group", is_administrative)
            VALUES ('350a73d2-1590-feda-db71-176204e0c026', 'tickets:cancel', 'Cancelar chamados',
                    'Cancelar qualquer chamado ativo, a qualquer momento, inclusive depois da janela em que o solicitante pode cancelar o próprio.',
                    'Chamados', TRUE),
                   ('c7eee483-2e4d-7b37-9c41-d3a318596af0', 'tickets:delete', 'Excluir chamados',
                    'Excluir chamados ativos de qualquer colaborador.', 'Chamados', TRUE)
            ON CONFLICT DO NOTHING;

            INSERT INTO public.role_permissions (id, role_name, permission_code)
            VALUES ('0ffd1d86-7c1f-76be-fcc8-78e1b0056ef6', 'Administrador', 'tickets:cancel'),
                   ('29c29b02-a075-3856-0c6b-4cdeb4704702', 'Administrador', 'tickets:delete')
            ON CONFLICT DO NOTHING;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReplacePermissionsSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Pelo código, e não pelo id: os vínculos podem ter sido concedidos a outras roles pela API.
            migrationBuilder.Sql(
                """
                DELETE FROM public.role_permissions WHERE permission_code IN ('tickets:cancel', 'tickets:delete');
                DELETE FROM public.permissions WHERE code IN ('tickets:cancel', 'tickets:delete');

                INSERT INTO public.permissions (id, code, name, description, "group", is_administrative)
                VALUES ('76583caa-5504-32d4-a843-7fff15e6ab2b', 'tickets:delete-own', 'Excluir os próprios chamados',
                        'Excluir os chamados que a própria pessoa abriu.', 'Chamados', FALSE)
                ON CONFLICT DO NOTHING;

                INSERT INTO public.role_permissions (id, role_name, permission_code)
                VALUES ('b65a9c90-5695-a8d9-44d1-6e7a494af14b', 'Administrador', 'tickets:delete-own'),
                       ('679ee996-9f28-3f75-24e2-531ed4f8f374', 'Atendente', 'tickets:delete-own'),
                       ('2760e0bf-81e7-dbf7-4790-6d1782cb78eb', 'Colaborador', 'tickets:delete-own')
                ON CONFLICT DO NOTHING;
                """);
        }
    }
}
