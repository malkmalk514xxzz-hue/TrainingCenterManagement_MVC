using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingCenterManagement_MVC.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAnthropicApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnthropicApiKey",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0001-0001-0001-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 6, 18, 14, 833, DateTimeKind.Utc).AddTicks(3456));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0002-0002-0002-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 6, 18, 14, 833, DateTimeKind.Utc).AddTicks(3470));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0003-0003-0003-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 6, 18, 14, 833, DateTimeKind.Utc).AddTicks(3475));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0004-0004-0004-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 6, 18, 14, 833, DateTimeKind.Utc).AddTicks(3477));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnthropicApiKey",
                table: "AspNetUsers");

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0001-0001-0001-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 5, 36, 1, 998, DateTimeKind.Utc).AddTicks(4409));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0002-0002-0002-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 5, 36, 1, 998, DateTimeKind.Utc).AddTicks(4423));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0003-0003-0003-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 5, 36, 1, 998, DateTimeKind.Utc).AddTicks(4429));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0004-0004-0004-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 5, 36, 1, 998, DateTimeKind.Utc).AddTicks(4431));
        }
    }
}
