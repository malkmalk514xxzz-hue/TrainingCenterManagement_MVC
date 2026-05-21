using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingCenterManagement_MVC.Migrations
{
    /// <inheritdoc />
    public partial class AddGroqModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroqModel",
                table: "AISystemConfigs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0001-0001-0001-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 20, 11, 44, 45, 557, DateTimeKind.Utc).AddTicks(8007));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0002-0002-0002-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 20, 11, 44, 45, 557, DateTimeKind.Utc).AddTicks(8023));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0003-0003-0003-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 20, 11, 44, 45, 557, DateTimeKind.Utc).AddTicks(8027));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0004-0004-0004-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 20, 11, 44, 45, 557, DateTimeKind.Utc).AddTicks(8031));

            migrationBuilder.UpdateData(
                table: "AISystemConfigs",
                keyColumn: "Id",
                keyValue: 1,
                column: "GroqModel",
                value: "llama-3.3-70b-versatile");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroqModel",
                table: "AISystemConfigs");

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0001-0001-0001-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 20, 11, 5, 50, 199, DateTimeKind.Utc).AddTicks(241));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0002-0002-0002-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 20, 11, 5, 50, 199, DateTimeKind.Utc).AddTicks(256));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0003-0003-0003-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 20, 11, 5, 50, 199, DateTimeKind.Utc).AddTicks(260));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0004-0004-0004-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 20, 11, 5, 50, 199, DateTimeKind.Utc).AddTicks(263));
        }
    }
}
