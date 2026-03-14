using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantSaaS.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddUserPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the "permissions" column to ALL existing tenant schemas.
            // We use a DO block to iterate through all tenant schemas and run ALTER TABLE.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    schema_name TEXT;
BEGIN
    FOR schema_name IN
        SELECT nspname
        FROM pg_namespace
        WHERE nspname LIKE 'tenant_%'
    LOOP
        IF EXISTS (
            SELECT 1
            FROM information_schema.tables
            WHERE table_schema = schema_name AND table_name = 'users'
        ) AND NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = schema_name AND table_name = 'users' AND column_name = 'permissions'
        ) THEN
            EXECUTE format('ALTER TABLE %I.users ADD COLUMN permissions TEXT NOT NULL DEFAULT ''''', schema_name);
        END IF;
    END LOOP;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
DECLARE
    schema_name TEXT;
BEGIN
    FOR schema_name IN
        SELECT nspname
        FROM pg_namespace
        WHERE nspname LIKE 'tenant_%'
    LOOP
        IF EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = schema_name AND table_name = 'users' AND column_name = 'permissions'
        ) THEN
            EXECUTE format('ALTER TABLE %I.users DROP COLUMN permissions', schema_name);
        END IF;
    END LOOP;
END $$;
");
        }
    }
}
