using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingCenterManagement_MVC.Migrations
{
    /// <inheritdoc />
    public partial class MakeLectureVideoTrainerIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LectureVideos_Trainers_UploadedByTrainerId",
                table: "LectureVideos");

            migrationBuilder.AlterColumn<Guid>(
                name: "UploadedByTrainerId",
                table: "LectureVideos",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddForeignKey(
                name: "FK_LectureVideos_Trainers_UploadedByTrainerId",
                table: "LectureVideos",
                column: "UploadedByTrainerId",
                principalTable: "Trainers",
                principalColumn: "TrainerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LectureVideos_Trainers_UploadedByTrainerId",
                table: "LectureVideos");

            migrationBuilder.AlterColumn<Guid>(
                name: "UploadedByTrainerId",
                table: "LectureVideos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LectureVideos_Trainers_UploadedByTrainerId",
                table: "LectureVideos",
                column: "UploadedByTrainerId",
                principalTable: "Trainers",
                principalColumn: "TrainerId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
