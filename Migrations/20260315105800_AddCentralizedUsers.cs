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
            migrationBuilder.DropPrimaryKey(
                name: "PK_Tenants",
                table: "Tenants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SuperAdmins",
                table: "SuperAdmins");

            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.RenameTable(
                name: "Tenants",
                newName: "tenants",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "SuperAdmins",
                newName: "super_admins",
                newSchema: "public");

            migrationBuilder.RenameColumn(
                name: "Name",
                schema: "public",
                table: "tenants",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Email",
                schema: "public",
                table: "tenants",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "public",
                table: "tenants",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "SchemaName",
                schema: "public",
                table: "tenants",
                newName: "schema_name");

            migrationBuilder.RenameColumn(
                name: "ReferenceCode",
                schema: "public",
                table: "tenants",
                newName: "reference_code");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                schema: "public",
                table: "tenants",
                newName: "password_hash");

            migrationBuilder.RenameColumn(
                name: "IsApproved",
                schema: "public",
                table: "tenants",
                newName: "is_approved");

            migrationBuilder.RenameColumn(
                name: "Username",
                schema: "public",
                table: "super_admins",
                newName: "username");

            migrationBuilder.RenameColumn(
                name: "Email",
                schema: "public",
                table: "super_admins",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "public",
                table: "super_admins",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                schema: "public",
                table: "super_admins",
                newName: "password_hash");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tenants",
                schema: "public",
                table: "tenants",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_super_admins",
                schema: "public",
                table: "super_admins",
                column: "id");

            migrationBuilder.CreateTable(
                name: "users",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    permissions = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });
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
