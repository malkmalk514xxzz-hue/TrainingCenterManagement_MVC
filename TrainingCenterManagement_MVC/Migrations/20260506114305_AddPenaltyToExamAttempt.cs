using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingCenterManagement_MVC.Migrations
{
    /// <inheritdoc />
    public partial class AddPenaltyToExamAttempt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OriginalScorePercentage",
                table: "ExamAttempts",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalTotalScore",
                table: "ExamAttempts",
                type: "decimal(7,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PenaltyApplied",
                table: "ExamAttempts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PenaltyReason",
                table: "ExamAttempts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalScorePercentage",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "OriginalTotalScore",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "PenaltyApplied",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "PenaltyReason",
                table: "ExamAttempts");
        }
    }
}
