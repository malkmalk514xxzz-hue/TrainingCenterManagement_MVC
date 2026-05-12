using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingCenterManagement_MVC.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CourseCurrency",
                table: "Courses",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CourseCurrency",
                table: "Courses");
        }
    }
}
