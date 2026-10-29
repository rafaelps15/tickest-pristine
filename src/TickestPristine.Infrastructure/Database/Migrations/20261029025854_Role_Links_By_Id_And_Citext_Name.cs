using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Spec 036: user_roles e role_permissions passam a referenciar a role pelo id, e o nome vira citext único (sem
    /// diferenciar maiúsculas) com até 100 caracteres. Escrita à mão: o EF apagaria role_name antes de existir role_id
    /// e perderia todos os vínculos, inclusive os concedidos pela API depois das seeds. Aqui role_id é preenchido a partir
    /// do nome antes de role_name sair; os vínculos semeados recebem o mesmo id do HasData nesse UPDATE, por isso os
    /// UpdateData gerados para eles saíram. O Down faz o caminho inverso, preenchendo role_name a partir do id.
    /// </remarks>
    public partial class Role_Links_By_Id_And_Citext_Name : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- Dois nomes iguais sem considerar maiúsculas quebrariam o índice único do citext: falha sem apagar nenhum.
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public.roles GROUP BY lower(name) HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Há funções com o mesmo nome sem diferenciar maiúsculas de minúsculas. Renomeie uma delas antes de aplicar a migration Role_Links_By_Id_And_Citext_Name.';
                    END IF;
                END $$;

                ALTER TABLE public.role_permissions DROP CONSTRAINT fk_role_permissions_roles_role_name;
                ALTER TABLE public.user_roles DROP CONSTRAINT fk_user_roles_roles_role_name;

                -- O SET NOT NULL falha (e a migration é desfeita) se algum vínculo não encontrar a role pelo nome.
                ALTER TABLE public.role_permissions ADD COLUMN role_id uuid;
                UPDATE public.role_permissions rp SET role_id = r.id FROM public.roles r WHERE r.name = rp.role_name;
                ALTER TABLE public.role_permissions ALTER COLUMN role_id SET NOT NULL;
                DROP INDEX public.ix_role_permissions_role_name_permission_code;
                ALTER TABLE public.role_permissions DROP COLUMN role_name;

                ALTER TABLE public.user_roles ADD COLUMN role_id uuid;
                UPDATE public.user_roles ur SET role_id = r.id FROM public.roles r WHERE r.name = ur.role_name;
                ALTER TABLE public.user_roles ALTER COLUMN role_id SET NOT NULL;
                DROP INDEX public.ix_user_roles_role_name;
                DROP INDEX public.ix_user_roles_user_id_role_name;
                ALTER TABLE public.user_roles DROP COLUMN role_name;

                ALTER TABLE public.roles DROP CONSTRAINT ak_roles_name;
                ALTER TABLE public.roles ALTER COLUMN name TYPE citext;
                ALTER TABLE public.roles ADD CONSTRAINT ck_roles_name_max_length CHECK (char_length(name) <= 100);
                CREATE UNIQUE INDEX ix_roles_name ON public.roles (name);

                CREATE UNIQUE INDEX ix_role_permissions_role_id_permission_code ON public.role_permissions (role_id, permission_code);
                CREATE INDEX ix_user_roles_role_id ON public.user_roles (role_id);
                CREATE UNIQUE INDEX ix_user_roles_user_id_role_id ON public.user_roles (user_id, role_id);

                ALTER TABLE public.role_permissions ADD CONSTRAINT fk_role_permissions_roles_role_id
                    FOREIGN KEY (role_id) REFERENCES public.roles (id) ON DELETE CASCADE;
                ALTER TABLE public.user_roles ADD CONSTRAINT fk_user_roles_roles_role_id
                    FOREIGN KEY (role_id) REFERENCES public.roles (id) ON DELETE RESTRICT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE public.role_permissions DROP CONSTRAINT fk_role_permissions_roles_role_id;
                ALTER TABLE public.user_roles DROP CONSTRAINT fk_user_roles_roles_role_id;
                DROP INDEX public.ix_role_permissions_role_id_permission_code;
                DROP INDEX public.ix_user_roles_role_id;
                DROP INDEX public.ix_user_roles_user_id_role_id;

                DROP INDEX public.ix_roles_name;
                ALTER TABLE public.roles DROP CONSTRAINT ck_roles_name_max_length;
                -- Falha (e o Down é desfeito) se alguma role tiver nome com mais de 50 caracteres, o limite anterior.
                ALTER TABLE public.roles ALTER COLUMN name TYPE character varying(50);
                ALTER TABLE public.roles ADD CONSTRAINT ak_roles_name UNIQUE (name);

                ALTER TABLE public.role_permissions ADD COLUMN role_name character varying(50);
                UPDATE public.role_permissions rp SET role_name = r.name FROM public.roles r WHERE r.id = rp.role_id;
                ALTER TABLE public.role_permissions ALTER COLUMN role_name SET NOT NULL;
                ALTER TABLE public.role_permissions DROP COLUMN role_id;

                ALTER TABLE public.user_roles ADD COLUMN role_name character varying(50);
                UPDATE public.user_roles ur SET role_name = r.name FROM public.roles r WHERE r.id = ur.role_id;
                ALTER TABLE public.user_roles ALTER COLUMN role_name SET NOT NULL;
                ALTER TABLE public.user_roles DROP COLUMN role_id;

                CREATE UNIQUE INDEX ix_role_permissions_role_name_permission_code ON public.role_permissions (role_name, permission_code);
                CREATE INDEX ix_user_roles_role_name ON public.user_roles (role_name);
                CREATE UNIQUE INDEX ix_user_roles_user_id_role_name ON public.user_roles (user_id, role_name);

                ALTER TABLE public.role_permissions ADD CONSTRAINT fk_role_permissions_roles_role_name
                    FOREIGN KEY (role_name) REFERENCES public.roles (name) ON DELETE CASCADE;
                ALTER TABLE public.user_roles ADD CONSTRAINT fk_user_roles_roles_role_name
                    FOREIGN KEY (role_name) REFERENCES public.roles (name) ON DELETE CASCADE;
                """);
        }
    }
}
