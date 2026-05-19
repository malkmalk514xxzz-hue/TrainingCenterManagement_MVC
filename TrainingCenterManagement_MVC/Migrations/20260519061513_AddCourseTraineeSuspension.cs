using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingCenterManagement_MVC.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseTraineeSuspension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EnrolledAt",
                table: "CourseTrainees",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                table: "CourseTrainees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                table: "CourseTrainees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                table: "CourseTrainees",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0001-0001-0001-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 19, 6, 15, 10, 147, DateTimeKind.Utc).AddTicks(447));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0002-0002-0002-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 19, 6, 15, 10, 147, DateTimeKind.Utc).AddTicks(461));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0003-0003-0003-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 19, 6, 15, 10, 147, DateTimeKind.Utc).AddTicks(464));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0004-0004-0004-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 19, 6, 15, 10, 147, DateTimeKind.Utc).AddTicks(467));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnrolledAt",
                table: "CourseTrainees");

            migrationBuilder.DropColumn(
                name: "IsSuspended",
                table: "CourseTrainees");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                table: "CourseTrainees");

            migrationBuilder.DropColumn(
                name: "SuspensionReason",
                table: "CourseTrainees");

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0001-0001-0001-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 18, 12, 47, 49, 913, DateTimeKind.Utc).AddTicks(1328));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0002-0002-0002-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 18, 12, 47, 49, 913, DateTimeKind.Utc).AddTicks(1343));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0003-0003-0003-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 18, 12, 47, 49, 913, DateTimeKind.Utc).AddTicks(1347));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0004-0004-0004-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 18, 12, 47, 49, 913, DateTimeKind.Utc).AddTicks(1350));
        }
    }
}
