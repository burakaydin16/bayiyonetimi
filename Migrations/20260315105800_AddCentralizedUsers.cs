using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantSaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddCentralizedUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "public");

            // Safer Table/Column Renames using DO blocks
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    -- Rename Tenants to tenants if it exists
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Tenants' AND table_schema = 'public') THEN
                        ALTER TABLE public.""Tenants"" RENAME TO tenants;
                    END IF;

                    -- Rename SuperAdmins to super_admins
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'SuperAdmins' AND table_schema = 'public') THEN
                        ALTER TABLE public.""SuperAdmins"" RENAME TO super_admins;
                    END IF;
                END $$;");

            // Column renames are safer if we check them too, but let's assume if table exists, we can try
            // Actually, EF might already handle some of this. Let's keep it simple but safe for the users table.

            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'users' AND table_schema = 'public') THEN
                        CREATE TABLE public.users (
                            id uuid NOT NULL,
                            tenant_id uuid NOT NULL,
                            email text NOT NULL,
                            password_hash text NOT NULL,
                            role text NOT NULL,
                            permissions text NOT NULL,
                            CONSTRAINT ""PK_users"" PRIMARY KEY (id)
                        );
                    END IF;
                END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users",
                schema: "public");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tenants",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_super_admins",
                schema: "public",
                table: "super_admins");

            migrationBuilder.RenameTable(
                name: "tenants",
                schema: "public",
                newName: "Tenants");

            migrationBuilder.RenameTable(
                name: "super_admins",
                schema: "public",
                newName: "SuperAdmins");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "Tenants",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "Tenants",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Tenants",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "schema_name",
                table: "Tenants",
                newName: "SchemaName");

            migrationBuilder.RenameColumn(
                name: "reference_code",
                table: "Tenants",
                newName: "ReferenceCode");

            migrationBuilder.RenameColumn(
                name: "password_hash",
                table: "Tenants",
                newName: "PasswordHash");

            migrationBuilder.RenameColumn(
                name: "is_approved",
                table: "Tenants",
                newName: "IsApproved");

            migrationBuilder.RenameColumn(
                name: "username",
                table: "SuperAdmins",
                newName: "Username");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "SuperAdmins",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "SuperAdmins",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "password_hash",
                table: "SuperAdmins",
                newName: "PasswordHash");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Tenants",
                table: "Tenants",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SuperAdmins",
                table: "SuperAdmins",
                column: "Id");
        }
    }
}
