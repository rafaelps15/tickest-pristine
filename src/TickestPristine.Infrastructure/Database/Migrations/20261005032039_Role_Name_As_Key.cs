using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// A role passa a ser identificada pelo nome. Escrita à mão: o EF apagaria e recriaria roles e vínculos; aqui as
    /// colunas role_name são preenchidas a partir do id antes de ele sair, e nenhum dado é perdido.
    /// </remarks>
    public partial class Role_Name_As_Key : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE public.role_permissions DROP CONSTRAINT fk_role_permissions_roles_role_id;
                ALTER TABLE public.user_roles DROP CONSTRAINT fk_user_roles_roles_role_id;

                ALTER TABLE public.role_permissions ADD COLUMN role_name character varying(50);
                UPDATE public.role_permissions rp SET role_name = r.name FROM public.roles r WHERE r.id = rp.role_id;
                ALTER TABLE public.role_permissions ALTER COLUMN role_name SET NOT NULL;
                ALTER TABLE public.role_permissions DROP CONSTRAINT pk_role_permissions;
                ALTER TABLE public.role_permissions DROP COLUMN role_id;
                ALTER TABLE public.role_permissions ADD CONSTRAINT pk_role_permissions PRIMARY KEY (role_name, permission_code);

                ALTER TABLE public.user_roles ADD COLUMN role_name character varying(50);
                UPDATE public.user_roles ur SET role_name = r.name FROM public.roles r WHERE r.id = ur.role_id;
                ALTER TABLE public.user_roles ALTER COLUMN role_name SET NOT NULL;
                DROP INDEX public.ix_user_roles_role_id;
                DROP INDEX public.ix_user_roles_user_id_role_id;
                ALTER TABLE public.user_roles DROP COLUMN role_id;

                DROP INDEX public.ix_roles_name;
                ALTER TABLE public.roles DROP CONSTRAINT pk_roles;
                ALTER TABLE public.roles DROP COLUMN id;
                ALTER TABLE public.roles ADD CONSTRAINT pk_roles PRIMARY KEY (name);
                ALTER TABLE public.roles ALTER COLUMN version SET DEFAULT 0;
                -- Falha (e a migration é desfeita) se alguma role tiver nome com mais de 50 caracteres.
                ALTER TABLE public.roles ALTER COLUMN name TYPE character varying(50);

                CREATE INDEX ix_user_roles_role_name ON public.user_roles (role_name);
                CREATE UNIQUE INDEX ix_user_roles_user_id_role_name ON public.user_roles (user_id, role_name);

                ALTER TABLE public.role_permissions ADD CONSTRAINT fk_role_permissions_roles_role_name
                    FOREIGN KEY (role_name) REFERENCES public.roles (name) ON DELETE CASCADE;
                ALTER TABLE public.user_roles ADD CONSTRAINT fk_user_roles_roles_role_name
                    FOREIGN KEY (role_name) REFERENCES public.roles (name) ON DELETE CASCADE;
                """);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "permissions",
                keyColumn: "code",
                keyValue: "roles:manage",
                column: "description",
                value: "Definir as permissões de cada função.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "public",
                table: "permissions",
                keyColumn: "code",
                keyValue: "roles:manage",
                column: "description",
                value: "Criar funções e definir as permissões de cada uma.");

            // Devolve às roles do código os ids fixos que elas tinham; as demais recebem um id novo.
            migrationBuilder.Sql(
                """
                ALTER TABLE public.role_permissions DROP CONSTRAINT fk_role_permissions_roles_role_name;
                ALTER TABLE public.user_roles DROP CONSTRAINT fk_user_roles_roles_role_name;
                DROP INDEX public.ix_user_roles_role_name;
                DROP INDEX public.ix_user_roles_user_id_role_name;

                ALTER TABLE public.roles ALTER COLUMN version DROP DEFAULT;
                ALTER TABLE public.roles ALTER COLUMN name TYPE character varying(100);
                ALTER TABLE public.roles ADD COLUMN id uuid;
                UPDATE public.roles SET id = CASE name
                    WHEN 'Administrador' THEN '0199a7c4-0000-7000-8000-000000000001'::uuid
                    WHEN 'Atendente' THEN '0199a7c4-0000-7000-8000-000000000002'::uuid
                    WHEN 'Colaborador' THEN '0199a7c4-0000-7000-8000-000000000003'::uuid
                    ELSE gen_random_uuid() END;
                ALTER TABLE public.roles ALTER COLUMN id SET NOT NULL;
                ALTER TABLE public.roles DROP CONSTRAINT pk_roles;
                ALTER TABLE public.roles ADD CONSTRAINT pk_roles PRIMARY KEY (id);
                CREATE UNIQUE INDEX ix_roles_name ON public.roles (name);

                ALTER TABLE public.user_roles ADD COLUMN role_id uuid;
                UPDATE public.user_roles ur SET role_id = r.id FROM public.roles r WHERE r.name = ur.role_name;
                ALTER TABLE public.user_roles ALTER COLUMN role_id SET NOT NULL;
                ALTER TABLE public.user_roles DROP COLUMN role_name;
                CREATE INDEX ix_user_roles_role_id ON public.user_roles (role_id);
                CREATE UNIQUE INDEX ix_user_roles_user_id_role_id ON public.user_roles (user_id, role_id);

                ALTER TABLE public.role_permissions ADD COLUMN role_id uuid;
                UPDATE public.role_permissions rp SET role_id = r.id FROM public.roles r WHERE r.name = rp.role_name;
                ALTER TABLE public.role_permissions ALTER COLUMN role_id SET NOT NULL;
                ALTER TABLE public.role_permissions DROP CONSTRAINT pk_role_permissions;
                ALTER TABLE public.role_permissions DROP COLUMN role_name;
                ALTER TABLE public.role_permissions ADD CONSTRAINT pk_role_permissions PRIMARY KEY (role_id, permission_code);

                ALTER TABLE public.role_permissions ADD CONSTRAINT fk_role_permissions_roles_role_id
                    FOREIGN KEY (role_id) REFERENCES public.roles (id) ON DELETE CASCADE;
                ALTER TABLE public.user_roles ADD CONSTRAINT fk_user_roles_roles_role_id
                    FOREIGN KEY (role_id) REFERENCES public.roles (id) ON DELETE CASCADE;
                """);
        }
    }
}
