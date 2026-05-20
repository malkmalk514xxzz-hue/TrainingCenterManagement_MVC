using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingCenterManagement_MVC.Migrations
{
    /// <inheritdoc />
    public partial class addShamCashAccountCodeandTransferCodeandshamcashmodel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TransferCode",
                table: "Trainees",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "BalanceUSD",
                table: "Trainees",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BalanceSYP",
                table: "Trainees",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ShamCashAccountCode",
                table: "Trainers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShamCashAccountCode",
                table: "Receptionists",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "shamCashTranslation",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    userName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    transactionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    registrationDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    amountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    transactionType = table.Column<int>(type: "int", nullable: false),
                    currencyType = table.Column<int>(type: "int", nullable: false),
                    notes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shamCashTranslation", x => x.id);
                });

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0001-0001-0001-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 20, 16, 18, 49, 183, DateTimeKind.Utc).AddTicks(4559));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0002-0002-0002-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 20, 16, 18, 49, 183, DateTimeKind.Utc).AddTicks(4573));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0003-0003-0003-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 20, 16, 18, 49, 183, DateTimeKind.Utc).AddTicks(4576));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0004-0004-0004-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 20, 16, 18, 49, 183, DateTimeKind.Utc).AddTicks(4588));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shamCashTranslation");

            migrationBuilder.DropColumn(
                name: "TransferCode",
                table: "Trainees");

            migrationBuilder.DropColumn(
                name: "BalanceUSD",
                table: "Trainees");

            migrationBuilder.DropColumn(
                name: "BalanceSYP",
                table: "Trainees");

            migrationBuilder.DropColumn(
                name: "ShamCashAccountCode",
                table: "Trainers");

            migrationBuilder.DropColumn(
                name: "ShamCashAccountCode",
                table: "Receptionists");

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
    }
}
