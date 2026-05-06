using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingCenterManagement_MVC.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineExamSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Exams_Courses_CourseId",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Exams_CourseId",
                table: "Exams");

            migrationBuilder.RenameColumn(
                name: "ExamDate",
                table: "Exams",
                newName: "StartDateTime");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ExamName",
                table: "Exams",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Exams",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Exams",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "Exams",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "Exams",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRandomized",
                table: "Exams",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                table: "Exams",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "PassingScore",
                table: "Exams",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 60m);

            migrationBuilder.AddColumn<bool>(
                name: "ShowResultsImmediately",
                table: "Exams",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Exams",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<Guid>(
                name: "TrainerId",
                table: "Exams",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(@"
                UPDATE e
                SET
                    e.CreatedAt = ISNULL(e.StartDateTime, GETUTCDATE()),
                    e.UpdatedAt = ISNULL(e.StartDateTime, GETUTCDATE()),
                    e.IsPublished = 1,
                    e.IsRandomized = 0,
                    e.ShowResultsImmediately = 1,
                    e.DurationMinutes = CASE WHEN e.DurationMinutes = 0 THEN 60 ELSE e.DurationMinutes END,
                    e.PassingScore = CASE WHEN e.PassingScore = 0 THEN 60 ELSE e.PassingScore END,
                    e.MaxAttempts = CASE WHEN e.MaxAttempts = 0 THEN 1 ELSE e.MaxAttempts END,
                    e.TrainerId = COALESCE(
                        (
                            SELECT TOP (1) ct.TrainerId
                            FROM CourseTrainers ct
                            WHERE ct.CourseId = e.CourseId
                            ORDER BY ct.TrainerId
                        ),
                        (
                            SELECT TOP (1) t.TrainerId
                            FROM Trainers t
                            ORDER BY t.TrainerId
                        )
                    )
                FROM Exams e;
            ");

            migrationBuilder.CreateTable(
                name: "ExamAttempts",
                columns: table => new
                {
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TraineeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalScore = table.Column<decimal>(type: "decimal(7,2)", nullable: true),
                    MaxScore = table.Column<decimal>(type: "decimal(7,2)", nullable: true),
                    ScorePercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    IsPassed = table.Column<bool>(type: "bit", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TabSwitchCount = table.Column<int>(type: "int", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamAttempts", x => x.AttemptId);
                    table.ForeignKey(
                        name: "FK_ExamAttempts_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "ExamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamAttempts_Trainees_TraineeId",
                        column: x => x.TraineeId,
                        principalTable: "Trainees",
                        principalColumn: "TraineeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    QuestionType = table.Column<int>(type: "int", nullable: false),
                    DifficultyLevel = table.Column<int>(type: "int", nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrectAnswer = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Explanation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DefaultPoints = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TrainerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.QuestionId);
                    table.ForeignKey(
                        name: "FK_Questions_Trainers_TrainerId",
                        column: x => x.TrainerId,
                        principalTable: "Trainers",
                        principalColumn: "TrainerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamQuestions",
                columns: table => new
                {
                    ExamQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false),
                    PointsOverride = table.Column<decimal>(type: "decimal(5,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamQuestions", x => x.ExamQuestionId);
                    table.ForeignKey(
                        name: "FK_ExamQuestions_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "ExamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExamQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "QuestionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentAnswers",
                columns: table => new
                {
                    AnswerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnswerText = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: true),
                    PointsEarned = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TrainerFeedback = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AnsweredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsManuallyGraded = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAnswers", x => x.AnswerId);
                    table.ForeignKey(
                        name: "FK_StudentAnswers_ExamAttempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "ExamAttempts",
                        principalColumn: "AttemptId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentAnswers_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "QuestionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Exams_CourseId",
                table: "Exams",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_StartDateTime",
                table: "Exams",
                column: "StartDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_TrainerId_IsPublished",
                table: "Exams",
                columns: new[] { "TrainerId", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempt_ExamId_Status",
                table: "ExamAttempts",
                columns: new[] { "ExamId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAttempt_TraineeId",
                table: "ExamAttempts",
                column: "TraineeId");

            migrationBuilder.CreateIndex(
                name: "UX_ExamAttempt_Exam_Trainee_Number",
                table: "ExamAttempts",
                columns: new[] { "ExamId", "TraineeId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExamQuestions_QuestionId",
                table: "ExamQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "UX_ExamQuestion_ExamId_QuestionId",
                table: "ExamQuestions",
                columns: new[] { "ExamId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_TrainerId_Type",
                table: "Questions",
                columns: new[] { "TrainerId", "QuestionType" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnswer_AttemptId",
                table: "StudentAnswers",
                column: "AttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAnswers_QuestionId",
                table: "StudentAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "UX_StudentAnswer_Attempt_Question",
                table: "StudentAnswers",
                columns: new[] { "AttemptId", "QuestionId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_Courses_CourseId",
                table: "Exams",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "CourseId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_Trainers_TrainerId",
                table: "Exams",
                column: "TrainerId",
                principalTable: "Trainers",
                principalColumn: "TrainerId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Exams_Courses_CourseId",
                table: "Exams");

            migrationBuilder.DropForeignKey(
                name: "FK_Exams_Trainers_TrainerId",
                table: "Exams");

            migrationBuilder.DropTable(
                name: "ExamQuestions");

            migrationBuilder.DropTable(
                name: "StudentAnswers");

            migrationBuilder.DropTable(
                name: "ExamAttempts");

            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Exams_CourseId",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Exams_StartDateTime",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Exams_TrainerId_IsPublished",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "IsRandomized",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "PassingScore",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "ShowResultsImmediately",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "TrainerId",
                table: "Exams");

            migrationBuilder.RenameColumn(
                name: "StartDateTime",
                table: "Exams",
                newName: "ExamDate");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "ExamName",
                table: "Exams",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_Exams_CourseId",
                table: "Exams",
                column: "CourseId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_Courses_CourseId",
                table: "Exams",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "CourseId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
