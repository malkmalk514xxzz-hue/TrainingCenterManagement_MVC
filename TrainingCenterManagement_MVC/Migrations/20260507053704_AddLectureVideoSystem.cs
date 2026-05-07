using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrainingCenterManagement_MVC.Migrations
{
    /// <inheritdoc />
    public partial class AddLectureVideoSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LectureVideos",
                columns: table => new
                {
                    VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LectureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VideoTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    VideoSourceType = table.Column<int>(type: "int", nullable: false),
                    LocalFilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YouTubeVideoId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    VideoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: true),
                    FileSizeInBytes = table.Column<long>(type: "bigint", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UploadedByTrainerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LectureVideos", x => x.VideoId);
                    table.ForeignKey(
                        name: "FK_LectureVideos_Lectures_LectureId",
                        column: x => x.LectureId,
                        principalTable: "Lectures",
                        principalColumn: "LectureId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LectureVideos_Trainers_UploadedByTrainerId",
                        column: x => x.UploadedByTrainerId,
                        principalTable: "Trainers",
                        principalColumn: "TrainerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VideoViews",
                columns: table => new
                {
                    ViewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TraineeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WatchedSeconds = table.Column<int>(type: "int", nullable: false),
                    WatchPercentage = table.Column<double>(type: "float", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoViews", x => x.ViewId);
                    table.ForeignKey(
                        name: "FK_VideoViews_LectureVideos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "LectureVideos",
                        principalColumn: "VideoId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoViews_Trainees_TraineeId",
                        column: x => x.TraineeId,
                        principalTable: "Trainees",
                        principalColumn: "TraineeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LectureVideos_LectureId",
                table: "LectureVideos",
                column: "LectureId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureVideos_UploadedByTrainerId",
                table: "LectureVideos",
                column: "UploadedByTrainerId");

            migrationBuilder.CreateIndex(
                name: "IX_LectureVideos_YouTubeVideoId",
                table: "LectureVideos",
                column: "YouTubeVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoView_TraineeId",
                table: "VideoViews",
                column: "TraineeId");

            migrationBuilder.CreateIndex(
                name: "UX_VideoView_VideoId_TraineeId",
                table: "VideoViews",
                columns: new[] { "VideoId", "TraineeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoViews");

            migrationBuilder.DropTable(
                name: "LectureVideos");
        }
    }
}
