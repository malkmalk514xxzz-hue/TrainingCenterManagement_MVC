using Messaging_Chat_Application_MahmoudHakim.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.Services;
using TrainingCenterManagement_MVC.ViewModels;


namespace TrainingCenterManagement_MVC.Controllers
{
    public class LecturesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILectureResourceService _resourceService;
        private readonly IWebHostEnvironment _env;

        public LecturesController(ApplicationDbContext context, IHubContext<ChatHub> hubContext,
            ILectureResourceService resourceService, IWebHostEnvironment env)
        {
            _context = context;
            _hubContext = hubContext;
            _resourceService = resourceService;
            _env = env;
        }

        // GET: Lectures
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Index(
                                         Guid? courseId = null,int page = 1, int pageSize = 9, string search = "")
        {
            pageSize = Math.Clamp(pageSize, 6, 30);

            // ── Step 1: load all courses for the right-hand selector ───────
            var courses = await _context.Courses
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.CourseName)
                .ToListAsync();

            if (!courses.Any())
                return View(new LecturesIndexViewModel { Courses = courses });

            // ── Lecture counts per course (for the badges in the selector) ─
            var lectureCounts = await _context.Lectures
                .Where(l => !l.IsDeleted && courses.Select(c => c.CourseId).Contains(l.CourseId))
                .GroupBy(l => l.CourseId)
                .Select(g => new { CourseId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CourseId, x => x.Count);

            ViewBag.LectureCounts = lectureCounts;

            // ── Selected course (default = first course) ───────────────────
            var selectedCourseId = courseId ?? courses.First().CourseId;
            var selectedCourse = courses.FirstOrDefault(c => c.CourseId == selectedCourseId) ?? courses.First();
            selectedCourseId = selectedCourse.CourseId;

            // ── Step 2: load lectures for the selected course (paged) ──────
            var query = _context.Lectures
                .Where(l => l.CourseId == selectedCourseId && !l.IsDeleted)
                .Include(l => l.Videos)
                .Include(l => l.Resources)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(l =>
                    l.Title.ToLower().Contains(term) ||
                    (l.Description != null && l.Description.ToLower().Contains(term)));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var lectures = await query
                .OrderByDescending(l => l.LectureDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.SelectedCourseId = selectedCourseId;
            ViewBag.SelectedCourse = selectedCourse;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;

            // ── Ajax: return only the lectures panel ────────────────────────
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_LecturesPanelPartial", lectures);

            var model = new LecturesIndexViewModel
            {
                Courses = courses,
                Lectures = lectures
            };
            return View(model);
        }


        // GET: Lectures/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var lecture = await _context.Lectures
                .Include(l => l.Course)
                .Include(l => l.Videos.OrderBy(v => v.DisplayOrder))
                .Include(l => l.Materials.OrderBy(m => m.CreatedAt))
                .Include(l => l.Resources.Where(r => !r.IsDeleted).OrderBy(r => r.DisplayOrder))
                .FirstOrDefaultAsync(m => m.LectureId == id);

            if (lecture == null) return NotFound();

            // تسجيل حضور الطالب تلقائيًا إذا كان Trainee
            if (User.IsInRole("Trainee"))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainee == null)
                {
                    return BadRequest("Trainee record not found for this user.");
                }

                var traineeId = trainee.TraineeId;

                var nextLecture = await _context.Lectures
                      .Where(l => l.CourseId == lecture.CourseId && l.LectureDate > lecture.LectureDate)
                      .OrderBy(l => l.LectureDate)
                      .FirstOrDefaultAsync();

                var alreadyPresent = await _context.Presences
                    .AnyAsync(p => p.TraineeId == traineeId && p.LectureId == lecture.LectureId);


                if (nextLecture == null && !alreadyPresent)
                {
                    var presence = new Presence
                    {
                        PresenceId = Guid.NewGuid(),
                        LectureId = lecture.LectureId,
                        TraineeId = traineeId,
                        IsPresent = true
                    };

                    _context.Presences.Add(presence);
                    await _context.SaveChangesAsync();
                }
            }

            return View(lecture);
        }


        // GET: Lectures/Create
        [Authorize(Roles = "Trainer,Admin")]
        public IActionResult Create(Guid? courseId = null)
        {
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName", courseId);
            ViewData["PreselectedCourseId"] = courseId;
            return View();
        }

        // POST: Lectures/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("LectureId,Title,Description,LectureDate,IsDeleted,CourseId")] Lecture lecture,
            string? videoType,
            string? youtubeUrl,
            string? videoTitle,
            IFormFile? videoFile,
            IFormFile? thumbnailFile,
            List<IFormFile>? resourceFiles,
            List<string>? resourceTypes,
            List<string>? resourceDescriptions,
            List<bool>? resourceRequired)
        {
            lecture.LectureId = Guid.NewGuid();
            _context.Add(lecture);
            await _context.SaveChangesAsync();

            await SendLectureNotificationsAsync(lecture);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);
            var existingVideoCount = 0;

            // ── Handle video ───────────────────────────────────────────
            if (videoType == "youtube" && !string.IsNullOrWhiteSpace(youtubeUrl))
            {
                var ytId = ExtractYouTubeVideoId(youtubeUrl);
                if (!string.IsNullOrEmpty(ytId))
                {
                    _context.LectureVideos.Add(new LectureVideo
                    {
                        VideoId = Guid.NewGuid(),
                        LectureId = lecture.LectureId,
                        VideoTitle = string.IsNullOrWhiteSpace(videoTitle) ? lecture.Title : videoTitle.Trim(),
                        VideoSourceType = VideoSourceType.YouTube,
                        YouTubeVideoId = ytId,
                        VideoUrl = $"https://www.youtube.com/embed/{ytId}",
                        ThumbnailUrl = $"https://img.youtube.com/vi/{ytId}/mqdefault.jpg",
                        UploadedByTrainerId = trainer?.TrainerId,
                        DisplayOrder = ++existingVideoCount,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }
            }
            else if (videoType == "upload" && videoFile != null && videoFile.Length > 0)
            {
                var allowedExts = new[] { ".mp4", ".webm", ".ogg", ".mov", ".avi", ".mkv" };
                var ext = Path.GetExtension(videoFile.FileName).ToLowerInvariant();
                if (allowedExts.Contains(ext) && videoFile.Length <= 500L * 1024 * 1024)
                {
                    var fileName = $"{Guid.NewGuid()}{ext}";
                    var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "videos", lecture.LectureId.ToString());
                    Directory.CreateDirectory(uploadDir);
                    var fullPath = Path.Combine(uploadDir, fileName);
                    using var fs = new FileStream(fullPath, FileMode.Create);
                    await videoFile.CopyToAsync(fs);

                    // Save optional thumbnail image
                    string? thumbUrl = null;
                    if (thumbnailFile != null && thumbnailFile.Length > 0)
                    {
                        var allowedImgExts = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                        var imgExt = Path.GetExtension(thumbnailFile.FileName).ToLowerInvariant();
                        if (allowedImgExts.Contains(imgExt) && thumbnailFile.Length <= 5L * 1024 * 1024)
                        {
                            var thumbDir = Path.Combine(_env.WebRootPath, "uploads", "thumbnails", lecture.LectureId.ToString());
                            Directory.CreateDirectory(thumbDir);
                            var thumbName = $"{Guid.NewGuid()}{imgExt}";
                            using var ts = new FileStream(Path.Combine(thumbDir, thumbName), FileMode.Create);
                            await thumbnailFile.CopyToAsync(ts);
                            thumbUrl = $"/uploads/thumbnails/{lecture.LectureId}/{thumbName}";
                        }
                    }

                    _context.LectureVideos.Add(new LectureVideo
                    {
                        VideoId = Guid.NewGuid(),
                        LectureId = lecture.LectureId,
                        VideoTitle = string.IsNullOrWhiteSpace(videoTitle) ? lecture.Title : videoTitle.Trim(),
                        VideoSourceType = VideoSourceType.Uploaded,
                        LocalFilePath = fullPath,
                        VideoUrl = $"/uploads/videos/{lecture.LectureId}/{fileName}",
                        ThumbnailUrl = thumbUrl,
                        FileSizeInBytes = videoFile.Length,
                        UploadedByTrainerId = trainer?.TrainerId,
                        DisplayOrder = ++existingVideoCount,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();
                }
            }

            // ── Handle resource files ───────────────────────────────────
            if (resourceFiles != null && resourceFiles.Count > 0)
            {
                for (int i = 0; i < resourceFiles.Count; i++)
                {
                    var file = resourceFiles[i];
                    if (file == null || file.Length == 0) continue;

                    var rType = ResourceType.Other;
                    if (resourceTypes != null && i < resourceTypes.Count
                        && Enum.TryParse<ResourceType>(resourceTypes[i], out var parsedType))
                        rType = parsedType;

                    var desc = (resourceDescriptions != null && i < resourceDescriptions.Count)
                        ? resourceDescriptions[i] : null;

                    var isReq = (resourceRequired != null && i < resourceRequired.Count)
                        && resourceRequired[i];

                    try
                    {
                        await _resourceService.UploadResourceAsync(file, lecture.LectureId, trainer?.TrainerId, rType, desc, isReq);
                    }
                    catch { /* skip invalid files silently */ }
                }
            }

            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName", lecture.CourseId);
            return RedirectToAction(nameof(ViewLecture), new { id = lecture.LectureId });
        }

        private async Task SendLectureNotificationsAsync(Lecture lecture)
        {
            var course = await _context.Courses.FindAsync(lecture.CourseId);
            if (course == null) return;

            var enrolledTrainees = await _context.CourseTrainees
                .Where(ct => ct.CourseId == lecture.CourseId)
                .Include(ct => ct.Trainee)
                .ToListAsync();

            var notifications = enrolledTrainees.Select(ct => new UserNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId = ct.Trainee.UserId,
                Title = "محاضرة جديدة",
                Message = $"تمت إضافة محاضرة جديدة '{lecture.Title}' في دورة {course.CourseName}",
                Type = NotificationType.LectureAdded,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                RelatedId = lecture.LectureId.ToString()
            }).ToList();

            if (notifications.Any())
            {
                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();

                foreach (var trainee in enrolledTrainees)
                {
                    var connections = await _context.UserConnections
                        .Where(c => c.UserId == trainee.Trainee.UserId && c.IsConnected)
                        .Select(c => c.ConnectionId)
                        .ToListAsync();

                    foreach (var connId in connections)
                    {
                        await _hubContext.Clients.Client(connId).SendAsync(
                            "ReceiveSystemNotification",
                            "محاضرة جديدة",
                            $"تمت إضافة محاضرة جديدة '{lecture.Title}' في دورة {course.CourseName}");
                    }
                }
            }
        }

        // GET: Lectures/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var lecture = await _context.Lectures.FindAsync(id);
            if (lecture == null)
            {
                return NotFound();
            }
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName", lecture.CourseId);
            return View(lecture);
        }

        // POST: Lectures/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("LectureId,Title,Description,VideoUrl,ThumbnailUrl,LectureDate,IsDeleted,CourseId")] Lecture lecture)
        {
            if (id != lecture.LectureId)
            {
                return NotFound();
            }

            try
            {
                _context.Update(lecture);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LectureExists(lecture.LectureId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName", lecture.CourseId);

            return RedirectToAction(nameof(Index));
        }

        // GET: Lectures/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var lecture = await _context.Lectures
                .Include(l => l.Course)
                .FirstOrDefaultAsync(m => m.LectureId == id);
            if (lecture == null)
            {
                return NotFound();
            }

            return View(lecture);
        }

        // POST: Lectures/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var lecture = await _context.Lectures.FindAsync(id);
            if (lecture != null)
            {
                _context.Lectures.Remove(lecture);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool LectureExists(Guid id)
        {
            return _context.Lectures.Any(e => e.LectureId == id);
        }

        // GET: Lectures/UploadVideo/5
        public async Task<IActionResult> UploadVideo(Guid? id)
        {
            if (id == null) return NotFound();

            var lecture = await _context.Lectures.FindAsync(id);
            if (lecture == null) return NotFound();

            return View(lecture);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadVideo(Guid id, string videoUrl, string thumbnailUrl)
        {
            var lecture = await _context.Lectures.FindAsync(id);
            if (lecture == null) return NotFound();

            var videoId = ExtractYouTubeVideoId(videoUrl);
            if (string.IsNullOrEmpty(videoId))
            {
                ModelState.AddModelError("", "Invalid YouTube URL");
                return View(lecture);
            }

            lecture.VideoUrl = videoId;
            lecture.ThumbnailUrl = thumbnailUrl;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id });
        }

        private string ExtractYouTubeVideoId(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                var uri = new Uri(url);

                if (uri.Host.Contains("youtu.be"))
                {
                    return uri.AbsolutePath.Trim('/');
                }

                if (uri.Host.Contains("youtube.com"))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                    return query["v"];
                }
            }
            catch
            {
            }

            return null;
        }

        // GET: Lectures/ViewLecture/5
        public async Task<IActionResult> ViewLecture(Guid? id)
        {
            if (id == null) return NotFound();

            var lecture = await _context.Lectures
                  .Include(l => l.Course)
                  .Include(l => l.Presences)
                      .ThenInclude(p => p.Trainee)
                      .ThenInclude(t => t.User)
                  .FirstOrDefaultAsync(l => l.LectureId == id);

            if (lecture == null) return NotFound();

            if (User.IsInRole("Trainee"))
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainee == null)
                {
                    return BadRequest("Trainee record not found for this user.");
                }

                var traineeId = trainee.TraineeId;

                var alreadyPresent = await _context.Presences
                    .AnyAsync(p => p.TraineeId == traineeId && p.LectureId == lecture.LectureId);

                if (!alreadyPresent)
                {
                    var presence = new Presence
                    {
                        PresenceId = Guid.NewGuid(),
                        LectureId = lecture.LectureId,
                        TraineeId = traineeId,
                        IsPresent = true
                    };

                    _context.Presences.Add(presence);
                    await _context.SaveChangesAsync();
                }
            }

            var lecture2 = await _context.Lectures
                  .Include(l => l.Course)
                  .Include(l => l.Videos.OrderBy(v => v.DisplayOrder))
                  .Include(l => l.Resources.Where(r => !r.IsDeleted && r.IsVisible).OrderBy(r => r.DisplayOrder))
                  .Include(l => l.Presences)
                      .ThenInclude(p => p.Trainee)
                      .ThenInclude(t => t.User)
                  .FirstOrDefaultAsync(l => l.LectureId == id);
            if (lecture2 == null) return NotFound();

            return View(lecture2);
        }


        // GET: Lectures/MarkAttendance/5
        public async Task<IActionResult> MarkAttendance(Guid? id)
        {
            if (id == null) return NotFound();

            var lecture = await _context.Lectures
                .Include(l => l.Course)
                    .ThenInclude(c => c.CourseTrainees)
                        .ThenInclude(ct => ct.Trainee)
                        .ThenInclude(t => t.User)
                .Include(l => l.Presences)
                .FirstOrDefaultAsync(l => l.LectureId == id);

            if (lecture == null) return NotFound();

            var model = lecture.Course.CourseTrainees.Select(ct => new AttendanceViewModel
            {
                TraineeId = ct.Trainee.TraineeId,
                FullName = ct.Trainee.User.FullName,
                IsPresent = lecture.Presences.Any(p => p.TraineeId == ct.Trainee.TraineeId && p.IsPresent)
            }).ToList();

            ViewBag.LectureId = lecture.LectureId;
            ViewBag.LectureTitle = lecture.Title;

            if (lecture.LectureDate < DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = "Cannot mark attendance for a past lecture.";
                return RedirectToAction(nameof(ViewLecture), new { id = lecture.LectureId });
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer")]
        public async Task<IActionResult> MarkAttendance(Guid lectureId, List<AttendanceViewModel> attendanceList)
        {
            var existingPresences = await _context.Presences
                .Where(p => p.LectureId == lectureId)
                .ToListAsync();

            _context.Presences.RemoveRange(existingPresences);

            foreach (var record in attendanceList)
            {
                _context.Presences.Add(new Presence
                {
                    PresenceId = Guid.NewGuid(),
                    LectureId = lectureId,
                    TraineeId = record.TraineeId,
                    IsPresent = record.IsPresent
                });
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Attendance has been saved successfully.";
            return RedirectToAction(nameof(ViewLecture), new { id = lectureId });
        }

        // ── CourseLectures (Admin) — Pagination + Ajax + Search + Sort ────────

        [Authorize(Roles = "Admin,Trainer")]
        [HttpGet]
        [Route("Lectures/CourseLectures/{courseId:guid}")]
        public async Task<IActionResult> CourseLectures(
            Guid courseId,
            int page = 1,
            string search = "",
            string sortBy = "date",   // "date" | "title"
            string sortDir = "desc",   // "asc"  | "desc"
            int pageSize = 8)
        {
            // Clamp pageSize to reasonable range
            pageSize = Math.Clamp(pageSize, 5, 50);

            // ── Load course info for the header ───────────────────────────────
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound();

            // ── Build base query ──────────────────────────────────────────────
            var query = _context.Lectures
                .Where(l => l.CourseId == courseId && !l.IsDeleted)
                .Include(l => l.Videos)
                .Include(l => l.Resources)
                .AsQueryable();

            // ── Search ────────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(l =>
                    l.Title.ToLower().Contains(term) ||
                    (l.Description != null && l.Description.ToLower().Contains(term)));
            }

            // ── Count before paging ───────────────────────────────────────────
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // ── Sort ──────────────────────────────────────────────────────────
            query = (sortBy, sortDir) switch
            {
                ("title", "asc") => query.OrderBy(l => l.Title),
                ("title", "desc") => query.OrderByDescending(l => l.Title),
                ("date", "asc") => query.OrderBy(l => l.LectureDate),
                _ => query.OrderByDescending(l => l.LectureDate)  // default
            };

            // ── Page ──────────────────────────────────────────────────────────
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var lectures = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ── Pass data to view ─────────────────────────────────────────────
            ViewBag.CourseId = courseId;
            ViewBag.CourseName = course.CourseName;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.Search = search;
            ViewBag.SortBy = sortBy;
            ViewBag.SortDir = sortDir;
            ViewBag.PageSize = pageSize;

            // ── Ajax: return only the partial ─────────────────────────────────
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_LecturesTablePartial", lectures);

            return View(lectures);
        }

        // ── Soft-delete a lecture via Ajax (Admin) ────────────────────────────

        [Authorize(Roles = "Admin,Trainer")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SoftDelete(Guid id, Guid courseId)
        {
            var lecture = await _context.Lectures.FindAsync(id);
            if (lecture == null) return NotFound();

            lecture.IsDeleted = true;
            _context.Lectures.Update(lecture);
            await _context.SaveChangesAsync();

            // Return fresh partial for Ajax callers
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return await CourseLectures(courseId);
            }

            return RedirectToAction(nameof(CourseLectures), new { courseId });
        }

    }
}
