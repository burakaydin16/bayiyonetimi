using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantSaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdminResetToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "password_reset_token",
                schema: "public",
                table: "super_admins",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "password_reset_token_expiry",
                schema: "public",
                table: "super_admins",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.InsertData(
                schema: "public",
                table: "super_admins",
                columns: new[] { "id", "email", "password_hash", "password_reset_token", "password_reset_token_expiry", "username" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "bayiyonetimi016@gmail.com", "$2a$11$fAnf5t7MShhMEkkHHPZ1SOO9V4bcaDQSOWHP7al1d/X.LkHY4N4XO", null, null, "superadmin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "public",
                table: "super_admins",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.DropColumn(
                name: "password_reset_token",
                schema: "public",
                table: "super_admins");

            migrationBuilder.DropColumn(
                name: "password_reset_token_expiry",
                schema: "public",
                table: "super_admins");
        }
    }
}
