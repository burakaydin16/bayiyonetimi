using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantSaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdminAndTenantReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns to the existing "tenants" table (already lowercase in DB from initial migration mapping)
            migrationBuilder.AddColumn<bool>(
                name: "is_approved",
                schema: "public",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "reference_code",
                schema: "public",
                table: "tenants",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Create the super_admins table
            migrationBuilder.CreateTable(
                name: "super_admins",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_super_admins", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "super_admins", schema: "public");

            migrationBuilder.DropColumn(name: "is_approved", schema: "public", table: "tenants");
            migrationBuilder.DropColumn(name: "reference_code", schema: "public", table: "tenants");
        }
    }
}
