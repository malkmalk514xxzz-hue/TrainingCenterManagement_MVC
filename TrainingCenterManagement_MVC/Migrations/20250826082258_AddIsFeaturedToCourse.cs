using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingCenterManagement_MVC.Migrations
{
    /// <inheritdoc />
    public partial class AddIsFeaturedToCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "Courses",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "Courses");
        }
    }
}
