using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingCenterManagement_MVC.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptExtractedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RcptAmount",
                table: "PaymentRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RcptOperationNumber",
                table: "PaymentRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RcptPaymentDate",
                table: "PaymentRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RcptRecipientAccount",
                table: "PaymentRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RcptRecipientName",
                table: "PaymentRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RcptSenderName",
                table: "PaymentRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0001-0001-0001-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 19, 8, 9, 8, 340, DateTimeKind.Utc).AddTicks(3895));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0002-0002-0002-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 19, 8, 9, 8, 340, DateTimeKind.Utc).AddTicks(3953));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0003-0003-0003-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 19, 8, 9, 8, 340, DateTimeKind.Utc).AddTicks(3957));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0004-0004-0004-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 19, 8, 9, 8, 340, DateTimeKind.Utc).AddTicks(3959));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RcptAmount",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "RcptOperationNumber",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "RcptPaymentDate",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "RcptRecipientAccount",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "RcptRecipientName",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "RcptSenderName",
                table: "PaymentRequests");

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0001-0001-0001-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 19, 6, 45, 4, 969, DateTimeKind.Utc).AddTicks(1428));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0002-0002-0002-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 19, 6, 45, 4, 969, DateTimeKind.Utc).AddTicks(1439));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0003-0003-0003-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 19, 6, 45, 4, 969, DateTimeKind.Utc).AddTicks(1444));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0004-0004-0004-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 19, 6, 45, 4, 969, DateTimeKind.Utc).AddTicks(1446));
        }
    }
}
