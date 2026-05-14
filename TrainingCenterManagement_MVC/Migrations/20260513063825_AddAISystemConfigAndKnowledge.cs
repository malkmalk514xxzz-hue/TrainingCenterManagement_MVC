using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingCenterManagement_MVC.Migrations
{
    /// <inheritdoc />
    public partial class AddAISystemConfigAndKnowledge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnthropicApiKey",
                table: "AspNetUsers");

            migrationBuilder.CreateTable(
                name: "AIKnowledgeEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIKnowledgeEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AISystemConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Provider = table.Column<int>(type: "int", nullable: false),
                    OllamaUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OllamaModel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaxTokensPerResponse = table.Column<int>(type: "int", nullable: false),
                    SystemDailyLimit = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AISystemConfigs", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0001-0001-0001-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 6, 38, 25, 257, DateTimeKind.Utc).AddTicks(2110));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0002-0002-0002-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 6, 38, 25, 257, DateTimeKind.Utc).AddTicks(2123));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0003-0003-0003-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 6, 38, 25, 257, DateTimeKind.Utc).AddTicks(2127));

            migrationBuilder.UpdateData(
                table: "AIPermissionRoles",
                keyColumn: "PermissionId",
                keyValue: new Guid("a1b2c3d4-0004-0004-0004-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 6, 38, 25, 257, DateTimeKind.Utc).AddTicks(2130));

            migrationBuilder.InsertData(
                table: "AISystemConfigs",
                columns: new[] { "Id", "IsEnabled", "MaxTokensPerResponse", "OllamaModel", "OllamaUrl", "Provider", "SystemDailyLimit", "UpdatedAt", "UpdatedByUserId" },
                values: new object[] { 1, true, 1024, "llama3.2", "http://localhost:11434", 1, 500, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null });

            migrationBuilder.CreateIndex(
                name: "IX_AIKnowledgeEntries_IsActive",
                table: "AIKnowledgeEntries",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIKnowledgeEntries");

            migrationBuilder.DropTable(
                name: "AISystemConfigs");

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
    }
}
