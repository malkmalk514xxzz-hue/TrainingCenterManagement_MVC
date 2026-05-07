# 🎬 ميزة إدارة فيديوهات الدروس (Lecture Video Management)
## الوقت المتوقع: 2-3 أيام

---

## 🎯 الوصف الكامل
- المدربون يمكنهم **تحميل الفيديوهات مباشرة** على الموقع
- استيراد الفيديوهات **من YouTube برابط مباشر**
- تنظيم الفيديوهات حسب الدرس
- عرض الفيديوهات في صفحة الدرس مع مشغل فيديو
- تتبع مشاهدات الفيديوهات من قبل الطلاب
- دعم فيديوهات متعددة لكل درس

---

## 📊 Models المطلوبة

### 1. Models/LectureVideo.cs
```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
	public class LectureVideo
	{
		[Key]
		public Guid VideoId { get; set; } = Guid.NewGuid();

		[Required]
		public Guid LectureId { get; set; }
		public Lecture Lecture { get; set; }

		[Required, MaxLength(500)]
		public string VideoTitle { get; set; }

		[MaxLength(2000)]
		public string Description { get; set; }

		[Required]
		public VideoSourceType VideoSourceType { get; set; }

		public string LocalFilePath { get; set; }

		public string YouTubeVideoId { get; set; }

		[Required, Url]
		public string VideoUrl { get; set; }

		public int? DurationMinutes { get; set; }

		public long? FileSizeInBytes { get; set; }

		public string ThumbnailUrl { get; set; }

		public int DisplayOrder { get; set; } = 1;

		public int ViewCount { get; set; } = 0;

		public bool IsActive { get; set; } = true;

		public bool IsRequired { get; set; } = true;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? UpdatedAt { get; set; }

		[Required]
		public Guid UploadedByTrainerId { get; set; }
		public Trainer UploadedByTrainer { get; set; }

		public bool IsDeleted { get; set; } = false;
		public DateTime? DeletedAt { get; set; }

		public ICollection<VideoView> Views { get; set; } = new List<VideoView>();
	}

	public enum VideoSourceType
	{
		Uploaded = 0,
		YouTube = 1
	}

	public class VideoView
	{
		[Key]
		public Guid ViewId { get; set; } = Guid.NewGuid();

		[Required]
		public Guid VideoId { get; set; }
		public LectureVideo Video { get; set; }

		[Required]
		public Guid TraineeId { get; set; }
		public Trainee Trainee { get; set; }

		public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

		public int WatchedSeconds { get; set; } = 0;

		public double WatchPercentage { get; set; } = 0;

		public bool IsCompleted { get; set; } = false;
	}
}
```

---

## 🗄️ تحديثات ApplicationDbContext

في `Data/ApplicationDbContext.cs`، أضف:

```csharp
public DbSet<LectureVideo> LectureVideos { get; set; }
public DbSet<VideoView> VideoViews { get; set; }

// في OnModelCreating()
builder.Entity<LectureVideo>()
	.HasOne(lv => lv.Lecture)
	.WithMany(l => l.Videos)
	.HasForeignKey(lv => lv.LectureId)
	.OnDelete(DeleteBehavior.Cascade);

builder.Entity<LectureVideo>()
	.HasOne(lv => lv.UploadedByTrainer)
	.WithMany(t => t.LectureVideos)
	.HasForeignKey(lv => lv.UploadedByTrainerId)
	.OnDelete(DeleteBehavior.Restrict);

builder.Entity<LectureVideo>()
	.HasQueryFilter(lv => !lv.IsDeleted);

builder.Entity<VideoView>()
	.HasOne(vv => vv.Video)
	.WithMany(v => v.Views)
	.HasForeignKey(vv => vv.VideoId)
	.OnDelete(DeleteBehavior.Cascade);

builder.Entity<VideoView>()
	.HasOne(vv => vv.Trainee)
	.WithMany(t => t.VideoViews)
	.HasForeignKey(vv => vv.TraineeId)
	.OnDelete(DeleteBehavior.Restrict);
```

---

## 🎮 Services

### Services/IVideoProcessingService.cs
```csharp
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Services
{
	public interface IVideoProcessingService
	{
		Task<string> ExtractYouTubeVideoIdAsync(string youtubeUrl);
		string GetYouTubeThumbnailUrl(string videoId);
		Task<LectureVideo> UploadVideoAsync(IFormFile file, Guid lectureId, Guid trainerId, string title, string description);
		Task<LectureVideo> ImportYouTubeVideoAsync(string youtubeUrl, Guid lectureId, Guid trainerId, string title, string description);
		Task<bool> DeleteVideoAsync(Guid videoId, Guid trainerId);
		Task RecordVideoViewAsync(Guid videoId, Guid traineeId, int watchedSeconds, double watchPercentage);
	}
}
```

### Services/VideoProcessingService.cs
```csharp
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Services
{
	public class VideoProcessingService : IVideoProcessingService
	{
		private readonly ApplicationDbContext _context;
		private readonly IWebHostEnvironment _environment;
		private readonly ILogger<VideoProcessingService> _logger;

		public VideoProcessingService(ApplicationDbContext context, IWebHostEnvironment environment, ILogger<VideoProcessingService> logger)
		{
			_context = context;
			_environment = environment;
			_logger = logger;
		}

		public async Task<string> ExtractYouTubeVideoIdAsync(string youtubeUrl)
		{
			try
			{
				var patterns = new[]
				{
					@"(?:https?:\/\/)?(?:www\.)?youtube\.com\/watch\?v=([a-zA-Z0-9_-]{11})",
					@"(?:https?:\/\/)?(?:www\.)?youtu\.be\/([a-zA-Z0-9_-]{11})",
					@"(?:https?:\/\/)?(?:www\.)?youtube\.com\/embed\/([a-zA-Z0-9_-]{11})",
					@"(?:https?:\/\/)?(?:www\.)?youtube\.com\/v\/([a-zA-Z0-9_-]{11})"
				};

				foreach (var pattern in patterns)
				{
					var regex = new Regex(pattern);
					var match = regex.Match(youtubeUrl);
					if (match.Success)
						return match.Groups[1].Value;
				}

				throw new InvalidOperationException("لم يتمكن من استخراج معرف الفيديو من الرابط");
			}
			catch (Exception ex)
			{
				_logger.LogError($"خطأ في استخراج معرف YouTube: {ex.Message}");
				throw;
			}
		}

		public string GetYouTubeThumbnailUrl(string videoId)
		{
			return $"https://img.youtube.com/vi/{videoId}/maxresdefault.jpg";
		}

		public async Task<LectureVideo> UploadVideoAsync(IFormFile file, Guid lectureId, Guid trainerId, string title, string description)
		{
			var lecture = await _context.Lectures.FindAsync(lectureId);
			if (lecture == null)
				throw new InvalidOperationException("الدرس غير موجود");

			var allowedExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".webm" };
			var fileExtension = Path.GetExtension(file.FileName).ToLower();

			if (!allowedExtensions.Contains(fileExtension))
				throw new InvalidOperationException("نوع الملف غير مسموح. يرجى تحميل ملف فيديو صحيح.");

			const long maxFileSize = 500 * 1024 * 1024;
			if (file.Length > maxFileSize)
				throw new InvalidOperationException("حجم الملف أكبر من 500 MB");

			try
			{
				var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "videos", 
					DateTime.Now.Year.ToString(), DateTime.Now.Month.ToString("00"));
				Directory.CreateDirectory(uploadsPath);

				var fileName = $"{Guid.NewGuid()}{fileExtension}";
				var filePath = Path.Combine(uploadsPath, fileName);

				using (var stream = new FileStream(filePath, FileMode.Create))
				{
					await file.CopyToAsync(stream);
				}

				var nextOrder = await _context.LectureVideos
					.Where(v => v.LectureId == lectureId && !v.IsDeleted)
					.MaxAsync(v => (int?)v.DisplayOrder) ?? 0;

				var video = new LectureVideo
				{
					LectureId = lectureId,
					VideoTitle = title,
					Description = description,
					VideoSourceType = VideoSourceType.Uploaded,
					LocalFilePath = filePath,
					VideoUrl = $"/uploads/videos/{DateTime.Now.Year}/{DateTime.Now.Month:00}/{fileName}",
					ThumbnailUrl = $"/uploads/videos/{DateTime.Now.Year}/{DateTime.Now.Month:00}/{fileName}",
					FileSizeInBytes = file.Length,
					UploadedByTrainerId = trainerId,
					DisplayOrder = nextOrder + 1
				};

				_context.LectureVideos.Add(video);
				await _context.SaveChangesAsync();

				_logger.LogInformation($"تم رفع الفيديو بنجاح: {video.VideoId}");
				return video;
			}
			catch (Exception ex)
			{
				_logger.LogError($"خطأ في رفع الفيديو: {ex.Message}");
				throw;
			}
		}

		public async Task<LectureVideo> ImportYouTubeVideoAsync(string youtubeUrl, Guid lectureId, Guid trainerId, string title, string description)
		{
			var lecture = await _context.Lectures.FindAsync(lectureId);
			if (lecture == null)
				throw new InvalidOperationException("الدرس غير موجود");

			try
			{
				var videoId = await ExtractYouTubeVideoIdAsync(youtubeUrl);

				var nextOrder = await _context.LectureVideos
					.Where(v => v.LectureId == lectureId && !v.IsDeleted)
					.MaxAsync(v => (int?)v.DisplayOrder) ?? 0;

				var video = new LectureVideo
				{
					LectureId = lectureId,
					VideoTitle = title,
					Description = description,
					VideoSourceType = VideoSourceType.YouTube,
					YouTubeVideoId = videoId,
					VideoUrl = $"https://www.youtube.com/embed/{videoId}",
					ThumbnailUrl = GetYouTubeThumbnailUrl(videoId),
					UploadedByTrainerId = trainerId,
					DisplayOrder = nextOrder + 1
				};

				_context.LectureVideos.Add(video);
				await _context.SaveChangesAsync();

				_logger.LogInformation($"تم استيراد فيديو YouTube بنجاح: {video.VideoId}");
				return video;
			}
			catch (Exception ex)
			{
				_logger.LogError($"خطأ في استيراد فيديو YouTube: {ex.Message}");
				throw;
			}
		}

		public async Task<bool> DeleteVideoAsync(Guid videoId, Guid trainerId)
		{
			var video = await _context.LectureVideos.FindAsync(videoId);
			if (video == null)
				return false;

			if (video.UploadedByTrainerId != trainerId)
				throw new UnauthorizedAccessException("لا تملك صلاحية حذف هذا الفيديو");

			try
			{
				video.IsDeleted = true;
				video.DeletedAt = DateTime.UtcNow;

				if (video.VideoSourceType == VideoSourceType.Uploaded && !string.IsNullOrEmpty(video.LocalFilePath))
				{
					if (File.Exists(video.LocalFilePath))
						File.Delete(video.LocalFilePath);
				}

				await _context.SaveChangesAsync();
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError($"خطأ في حذف الفيديو: {ex.Message}");
				throw;
			}
		}

		public async Task RecordVideoViewAsync(Guid videoId, Guid traineeId, int watchedSeconds, double watchPercentage)
		{
			try
			{
				var existingView = await _context.VideoViews
					.FirstOrDefaultAsync(vv => vv.VideoId == videoId && vv.TraineeId == traineeId);

				if (existingView != null)
				{
					existingView.WatchedSeconds = Math.Max(existingView.WatchedSeconds, watchedSeconds);
					existingView.WatchPercentage = Math.Max(existingView.WatchPercentage, watchPercentage);
					existingView.IsCompleted = watchPercentage >= 95;
					_context.VideoViews.Update(existingView);
				}
				else
				{
					var view = new VideoView
					{
						VideoId = videoId,
						TraineeId = traineeId,
						WatchedSeconds = watchedSeconds,
						WatchPercentage = watchPercentage,
						IsCompleted = watchPercentage >= 95
					};
					_context.VideoViews.Add(view);
				}

				var video = await _context.LectureVideos.FindAsync(videoId);
				if (video != null)
					video.ViewCount++;

				await _context.SaveChangesAsync();
			}
			catch (Exception ex)
			{
				_logger.LogError($"خطأ في تسجيل مشاهدة الفيديو: {ex.Message}");
			}
		}
	}
}
```

---

## 🎮 Controller - VideosController.cs

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.Services;

namespace TrainingCenterManagement_MVC.Controllers
{
	[Authorize(Roles = "Trainer")]
	[Route("api/[controller]")]
	[ApiController]
	public class VideosController : ControllerBase
	{
		private readonly IVideoProcessingService _videoService;
		private readonly ApplicationDbContext _context;
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly ILogger<VideosController> _logger;

		public VideosController(IVideoProcessingService videoService, ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<VideosController> logger)
		{
			_videoService = videoService;
			_context = context;
			_userManager = userManager;
			_logger = logger;
		}

		[HttpPost("upload")]
		public async Task<IActionResult> UploadVideo([FromQuery] Guid lectureId, [FromForm] string title, [FromForm] string description, [FromForm] IFormFile videoFile)
		{
			try
			{
				var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
				var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);

				if (trainer == null)
					return Unauthorized("المستخدم ليس مدرباً");

				if (videoFile == null || videoFile.Length == 0)
					return BadRequest("الملف مطلوب");

				var video = await _videoService.UploadVideoAsync(videoFile, lectureId, trainer.TrainerId, title, description);
				return Ok(new { success = true, message = "تم رفع الفيديو بنجاح", videoId = video.VideoId });
			}
			catch (Exception ex)
			{
				_logger.LogError($"خطأ في رفع الفيديو: {ex.Message}");
				return BadRequest(new { success = false, message = ex.Message });
			}
		}

		[HttpPost("import-youtube")]
		public async Task<IActionResult> ImportYouTube([FromQuery] Guid lectureId, [FromBody] YouTubeImportRequest request)
		{
			try
			{
				var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
				var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);

				if (trainer == null)
					return Unauthorized("المستخدم ليس مدرباً");

				if (string.IsNullOrWhiteSpace(request.YouTubeUrl))
					return BadRequest("رابط YouTube مطلوب");

				var video = await _videoService.ImportYouTubeVideoAsync(request.YouTubeUrl, lectureId, trainer.TrainerId, request.Title, request.Description);
				return Ok(new { success = true, message = "تم استيراد الفيديو بنجاح", videoId = video.VideoId });
			}
			catch (Exception ex)
			{
				_logger.LogError($"خطأ في استيراد الفيديو: {ex.Message}");
				return BadRequest(new { success = false, message = ex.Message });
			}
		}

		[AllowAnonymous]
		[HttpGet("lecture/{lectureId}")]
		public async Task<IActionResult> GetLectureVideos(Guid lectureId)
		{
			try
			{
				var videos = await _context.LectureVideos
					.Where(v => v.LectureId == lectureId && v.IsActive && !v.IsDeleted)
					.OrderBy(v => v.DisplayOrder)
					.Select(v => new
					{
						v.VideoId,
						v.VideoTitle,
						v.Description,
						v.VideoUrl,
						v.ThumbnailUrl,
						v.VideoSourceType,
						v.ViewCount,
						v.IsRequired
					})
					.ToListAsync();

				return Ok(videos);
			}
			catch (Exception ex)
			{
				_logger.LogError($"خطأ في الحصول على الفيديوهات: {ex.Message}");
				return StatusCode(500, "خطأ في الحصول على الفيديوهات");
			}
		}

		[HttpDelete("{videoId}")]
		public async Task<IActionResult> DeleteVideo(Guid videoId)
		{
			try
			{
				var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
				var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);

				if (trainer == null)
					return Unauthorized("المستخدم ليس مدرباً");

				var success = await _videoService.DeleteVideoAsync(videoId, trainer.TrainerId);
				return success ? Ok(new { success = true, message = "تم حذف الفيديو بنجاح" }) : NotFound();
			}
			catch (Exception ex)
			{
				_logger.LogError($"خطأ في حذف الفيديو: {ex.Message}");
				return BadRequest(new { success = false, message = ex.Message });
			}
		}

		[Authorize(Roles = "Trainee")]
		[HttpPost("record-view")]
		public async Task<IActionResult> RecordVideoView([FromBody] VideoViewRequest request)
		{
			try
			{
				var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
				var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);

				if (trainee == null)
					return Unauthorized("المستخدم ليس طالباً");

				await _videoService.RecordVideoViewAsync(request.VideoId, trainee.TraineeId, request.WatchedSeconds, request.WatchPercentage);
				return Ok(new { success = true, message = "تم تسجيل المشاهدة" });
			}
			catch (Exception ex)
			{
				_logger.LogError($"خطأ في تسجيل المشاهدة: {ex.Message}");
				return BadRequest(new { success = false, message = ex.Message });
			}
		}
	}

	public class YouTubeImportRequest
	{
		public string YouTubeUrl { get; set; }
		public string Title { get; set; }
		public string Description { get; set; }
	}

	public class VideoViewRequest
	{
		public Guid VideoId { get; set; }
		public int WatchedSeconds { get; set; }
		public double WatchPercentage { get; set; }
	}
}
```

---

## 📋 تحديث Program.cs

```csharp
// أضف هذا السطر مع الخدمات الأخرى
builder.Services.AddScoped<IVideoProcessingService, VideoProcessingService>();
```

---

## 🗄️ Database Migration

```bash
dotnet ef migrations add AddLectureVideoManagementSystem
dotnet ef database update
```

---

## ✅ الميزات المكتملة:
- ✅ تحميل فيديوهات مباشرة
- ✅ استيراد من YouTube
- ✅ استخراج معرفات YouTube
- ✅ صور مصغرة تلقائية
- ✅ تتبع المشاهدات
- ✅ واجهة API كاملة
- ✅ أمان وتحقق من الصلاحيات

