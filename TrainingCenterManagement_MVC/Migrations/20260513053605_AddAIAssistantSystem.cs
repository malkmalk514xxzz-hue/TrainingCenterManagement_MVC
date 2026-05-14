using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TrainingCenterManagement_MVC.Migrations
{
    /// <inheritdoc />
    public partial class AddAIAssistantSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIAccessLogs",
                columns: table => new
                {
                    LogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AccessType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResourceAccessed = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsAuthorized = table.Column<bool>(type: "bit", nullable: false),
                    DenialReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AccessedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIAccessLogs", x => x.LogId);
                    table.ForeignKey(
                        name: "FK_AIAccessLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AIChatMessages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AIResponse = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    QuestionType = table.Column<int>(type: "int", nullable: false),
                    IsAnswered = table.Column<bool>(type: "bit", nullable: false),
                    RequiresManualReview = table.Column<bool>(type: "bit", nullable: false),
                    ReviewReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DataAccessLog = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    UserFeedback = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsHelpful = table.Column<bool>(type: "bit", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIChatMessages", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_AIChatMessages_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AIPermissionRoles",
                columns: table => new
                {
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CanReadPersonalData = table.Column<bool>(type: "bit", nullable: false),
                    CanReadOtherUsersData = table.Column<bool>(type: "bit", nullable: false),
                    CanReadAdminData = table.Column<bool>(type: "bit", nullable: false),
                    CanModifyPersonalData = table.Column<bool>(type: "bit", nullable: false),
                    CanModifyOtherUsersData = table.Column<bool>(type: "bit", nullable: false),
                    DailyQueryLimit = table.Column<int>(type: "int", nullable: false),
                    AllowedDataCategories = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BlockedDataCategories = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CanAccessAdvancedFeatures = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIPermissionRoles", x => x.PermissionId);
                });

            migrationBuilder.InsertData(
                table: "AIPermissionRoles",
                columns: new[] { "PermissionId", "AllowedDataCategories", "BlockedDataCategories", "CanAccessAdvancedFeatures", "CanModifyOtherUsersData", "CanModifyPersonalData", "CanReadAdminData", "CanReadOtherUsersData", "CanReadPersonalData", "CreatedAt", "CreatedBy", "DailyQueryLimit", "Notes", "RoleName", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("a1b2c3d4-0001-0001-0001-000000000001"), "Courses,Lectures,Progress,Grades,Attendance", "AdminSettings,OtherUsersData,PaymentDetails", false, false, false, false, false, true, new DateTime(2026, 5, 13, 5, 36, 1, 998, DateTimeKind.Utc).AddTicks(4409), "System", 50, null, "Trainee", null },
                    { new Guid("a1b2c3d4-0002-0002-0002-000000000002"), "Courses,Lectures,MyCourseStudents,Grades,Analytics", "AdminSettings,OtherUsersData,PaymentDetails", true, false, false, false, false, true, new DateTime(2026, 5, 13, 5, 36, 1, 998, DateTimeKind.Utc).AddTicks(4423), "System", 100, null, "Trainer", null },
                    { new Guid("a1b2c3d4-0003-0003-0003-000000000003"), "All", "", true, false, false, true, true, true, new DateTime(2026, 5, 13, 5, 36, 1, 998, DateTimeKind.Utc).AddTicks(4429), "System", -1, null, "Admin", null },
                    { new Guid("a1b2c3d4-0004-0004-0004-000000000004"), "Courses,Enrollments,Payments,Students", "AdminSettings,Grades,ExamDetails", false, false, false, false, false, true, new DateTime(2026, 5, 13, 5, 36, 1, 998, DateTimeKind.Utc).AddTicks(4431), "System", 100, null, "Receptionist", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIAccessLogs_AccessedAt",
                table: "AIAccessLogs",
                column: "AccessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AIAccessLogs_UserId",
                table: "AIAccessLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AIChatMessages_CreatedAt",
                table: "AIChatMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AIChatMessages_UserId",
                table: "AIChatMessages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AIChatMessages_UserId_CreatedAt",
                table: "AIChatMessages",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_AIPermissionRoles_RoleName",
                table: "AIPermissionRoles",
                column: "RoleName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIAccessLogs");

            migrationBuilder.DropTable(
                name: "AIChatMessages");

            migrationBuilder.DropTable(
                name: "AIPermissionRoles");
        }
    }
}
