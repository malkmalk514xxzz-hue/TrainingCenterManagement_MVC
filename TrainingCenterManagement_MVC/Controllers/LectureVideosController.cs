using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize]
    public class LectureVideosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public LectureVideosController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Manage(Guid lectureId)
        {
            var lecture = await _context.Lectures
                .Include(l => l.Course)
                .Include(l => l.Videos.OrderBy(v => v.DisplayOrder))
                .FirstOrDefaultAsync(l => l.LectureId == lectureId);

            if (lecture == null) return NotFound();

            return View(lecture);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> AddYouTube(Guid lectureId, string youtubeUrl, string videoTitle, string? description)
        {
            var lecture = await _context.Lectures.FindAsync(lectureId);
            if (lecture == null) return NotFound();

            var ytVideoId = ExtractYouTubeVideoId(youtubeUrl);
            if (string.IsNullOrEmpty(ytVideoId))
            {
                TempData["Error"] = "رابط YouTube غير صالح. تأكد أن الرابط يحتوي على معرف الفيديو.";
                return RedirectToAction(nameof(Manage), new { lectureId });
            }

            if (string.IsNullOrWhiteSpace(videoTitle))
            {
                TempData["Error"] = "عنوان الفيديو مطلوب.";
                return RedirectToAction(nameof(Manage), new { lectureId });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);

            var existingCount = await _context.LectureVideos.CountAsync(v => v.LectureId == lectureId);

            var video = new LectureVideo
            {
                VideoId = Guid.NewGuid(),
                LectureId = lectureId,
                VideoTitle = videoTitle.Trim(),
                Description = description?.Trim(),
                VideoSourceType = VideoSourceType.YouTube,
                YouTubeVideoId = ytVideoId,
                VideoUrl = $"https://www.youtube.com/embed/{ytVideoId}",
                ThumbnailUrl = $"https://img.youtube.com/vi/{ytVideoId}/mqdefault.jpg",
                UploadedByTrainerId = trainer?.TrainerId,
                DisplayOrder = existingCount + 1,
                CreatedAt = DateTime.UtcNow
            };

            _context.LectureVideos.Add(video);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم إضافة فيديو YouTube بنجاح.";
            return RedirectToAction(nameof(Manage), new { lectureId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Upload(Guid lectureId, IFormFile videoFile, string videoTitle, string? description)
        {
            var lecture = await _context.Lectures.FindAsync(lectureId);
            if (lecture == null) return NotFound();

            if (videoFile == null || videoFile.Length == 0)
            {
                TempData["Error"] = "يرجى اختيار ملف فيديو.";
                return RedirectToAction(nameof(Manage), new { lectureId });
            }

            if (string.IsNullOrWhiteSpace(videoTitle))
            {
                TempData["Error"] = "عنوان الفيديو مطلوب.";
                return RedirectToAction(nameof(Manage), new { lectureId });
            }

            var allowedExtensions = new[] { ".mp4", ".webm", ".ogg", ".mov", ".avi", ".mkv" };
            var ext = Path.GetExtension(videoFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                TempData["Error"] = "صيغة الملف غير مدعومة. الصيغ المقبولة: mp4, webm, ogg, mov, avi, mkv";
                return RedirectToAction(nameof(Manage), new { lectureId });
            }

            if (videoFile.Length > 500L * 1024 * 1024)
            {
                TempData["Error"] = "حجم الملف يتجاوز الحد المسموح (500 ميغابايت).";
                return RedirectToAction(nameof(Manage), new { lectureId });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);

            var fileName = $"{Guid.NewGuid()}{ext}";
            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "videos", lectureId.ToString());
            Directory.CreateDirectory(uploadDir);
            var fullPath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await videoFile.CopyToAsync(stream);
            }

            var existingCount = await _context.LectureVideos.CountAsync(v => v.LectureId == lectureId);

            var video = new LectureVideo
            {
                VideoId = Guid.NewGuid(),
                LectureId = lectureId,
                VideoTitle = videoTitle.Trim(),
                Description = description?.Trim(),
                VideoSourceType = VideoSourceType.Uploaded,
                LocalFilePath = fullPath,
                VideoUrl = $"/uploads/videos/{lectureId}/{fileName}",
                FileSizeInBytes = videoFile.Length,
                UploadedByTrainerId = trainer?.TrainerId,
                DisplayOrder = existingCount + 1,
                CreatedAt = DateTime.UtcNow
            };

            _context.LectureVideos.Add(video);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم رفع الفيديو بنجاح.";
            return RedirectToAction(nameof(Manage), new { lectureId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Delete(Guid videoId)
        {
            var video = await _context.LectureVideos
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(v => v.VideoId == videoId);

            if (video == null) return NotFound();

            var lectureId = video.LectureId;

            if (video.VideoSourceType == VideoSourceType.Uploaded && !string.IsNullOrEmpty(video.LocalFilePath))
            {
                if (System.IO.File.Exists(video.LocalFilePath))
                    System.IO.File.Delete(video.LocalFilePath);
            }

            video.IsDeleted = true;
            video.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حذف الفيديو بنجاح.";
            return RedirectToAction(nameof(Manage), new { lectureId });
        }

        [HttpPost]
        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> RecordView(Guid videoId, int watchedSeconds, double watchPercentage)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainee == null) return Unauthorized();

            var existing = await _context.VideoViews
                .FirstOrDefaultAsync(vv => vv.VideoId == videoId && vv.TraineeId == trainee.TraineeId);

            if (existing != null)
            {
                existing.WatchedSeconds = Math.Max(existing.WatchedSeconds, watchedSeconds);
                existing.WatchPercentage = Math.Max(existing.WatchPercentage, watchPercentage);
                existing.IsCompleted = existing.WatchPercentage >= 90;
            }
            else
            {
                _context.VideoViews.Add(new VideoView
                {
                    ViewId = Guid.NewGuid(),
                    VideoId = videoId,
                    TraineeId = trainee.TraineeId,
                    WatchedSeconds = watchedSeconds,
                    WatchPercentage = watchPercentage,
                    IsCompleted = watchPercentage >= 90,
                    ViewedAt = DateTime.UtcNow
                });

                var vid = await _context.LectureVideos.FindAsync(videoId);
                if (vid != null) vid.ViewCount++;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        private static string? ExtractYouTubeVideoId(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                var uri = new Uri(url);
                if (uri.Host.Contains("youtu.be"))
                    return uri.AbsolutePath.Trim('/');
                if (uri.Host.Contains("youtube.com"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    return query["v"];
                }
            }
            catch { }
            return null;
        }
    }
}
