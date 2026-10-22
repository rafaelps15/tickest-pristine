﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TickestPristine.Infrastructure.Database.Migrations
{
    /// <summary>
    /// Roles, permissões e os vínculos entre elas ganham um id Guid como chave primária. O nome da role e o código da
    /// permissão continuam únicos e continuam sendo o que user_roles e role_permissions referenciam, então nenhuma
    /// linha é apagada: o gerado pelo EF (DeleteData + InsertData das roles) apagaria em cascata as roles dos usuários.
    /// Os ids das linhas existentes saem da mesma conta do DeterministicGuid, para baterem com o HasData.
    /// </summary>
    public partial class Add_Ids_To_Roles_And_Permissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            DropForeignKeys(migrationBuilder);

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                schema: "public",
                table: "roles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                schema: "public",
                table: "permissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                schema: "public",
                table: "role_permissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE public.roles
                SET id = encode(substring(sha256(convert_to('role:' || name, 'UTF8')) FROM 1 FOR 16), 'hex')::uuid;

                UPDATE public.permissions
                SET id = encode(substring(sha256(convert_to('permission:' || code, 'UTF8')) FROM 1 FOR 16), 'hex')::uuid;

                UPDATE public.role_permissions
                SET id = encode(
                    substring(sha256(convert_to('role-permission:' || role_name || ':' || permission_code, 'UTF8')) FROM 1 FOR 16),
                    'hex')::uuid;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                schema: "public",
                table: "roles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                schema: "public",
                table: "permissions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                schema: "public",
                table: "role_permissions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropPrimaryKey(
                name: "pk_roles",
                schema: "public",
                table: "roles");

            migrationBuilder.DropPrimaryKey(
                name: "pk_permissions",
                schema: "public",
                table: "permissions");

            migrationBuilder.DropPrimaryKey(
                name: "pk_role_permissions",
                schema: "public",
                table: "role_permissions");

            migrationBuilder.AddPrimaryKey(
                name: "pk_roles",
                schema: "public",
                table: "roles",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_permissions",
                schema: "public",
                table: "permissions",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_role_permissions",
                schema: "public",
                table: "role_permissions",
                column: "id");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_roles_name",
                schema: "public",
                table: "roles",
                column: "name");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_permissions_code",
                schema: "public",
                table: "permissions",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_role_name_permission_code",
                schema: "public",
                table: "role_permissions",
                columns: new[] { "role_name", "permission_code" },
                unique: true);

            AddForeignKeys(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropForeignKeys(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "ix_role_permissions_role_name_permission_code",
                schema: "public",
                table: "role_permissions");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_roles_name",
                schema: "public",
                table: "roles");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_permissions_code",
                schema: "public",
                table: "permissions");

            migrationBuilder.DropPrimaryKey(
                name: "pk_roles",
                schema: "public",
                table: "roles");

            migrationBuilder.DropPrimaryKey(
                name: "pk_permissions",
                schema: "public",
                table: "permissions");

            migrationBuilder.DropPrimaryKey(
                name: "pk_role_permissions",
                schema: "public",
                table: "role_permissions");

            migrationBuilder.DropColumn(
                name: "id",
                schema: "public",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "id",
                schema: "public",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "id",
                schema: "public",
                table: "role_permissions");

            migrationBuilder.AddPrimaryKey(
                name: "pk_roles",
                schema: "public",
                table: "roles",
                column: "name");

            migrationBuilder.AddPrimaryKey(
                name: "pk_permissions",
                schema: "public",
                table: "permissions",
                column: "code");

            migrationBuilder.AddPrimaryKey(
                name: "pk_role_permissions",
                schema: "public",
                table: "role_permissions",
                columns: new[] { "role_name", "permission_code" });

            AddForeignKeys(migrationBuilder);
        }

        private static void DropForeignKeys(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_roles_roles_role_name",
                schema: "public",
                table: "user_roles");

            migrationBuilder.DropForeignKey(
                name: "fk_role_permissions_roles_role_name",
                schema: "public",
                table: "role_permissions");

            migrationBuilder.DropForeignKey(
                name: "fk_role_permissions_permissions_permission_code",
                schema: "public",
                table: "role_permissions");
        }

        private static void AddForeignKeys(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "fk_user_roles_roles_role_name",
                schema: "public",
                table: "user_roles",
                column: "role_name",
                principalSchema: "public",
                principalTable: "roles",
                principalColumn: "name",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_role_permissions_roles_role_name",
                schema: "public",
                table: "role_permissions",
                column: "role_name",
                principalSchema: "public",
                principalTable: "roles",
                principalColumn: "name",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_role_permissions_permissions_permission_code",
                schema: "public",
                table: "role_permissions",
                column: "permission_code",
                principalSchema: "public",
                principalTable: "permissions",
                principalColumn: "code",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
