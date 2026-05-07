using Messaging_Chat_Application_MahmoudHakim.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;


namespace TrainingCenterManagement_MVC.Controllers
{
    public class LecturesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;

        public LecturesController(ApplicationDbContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // GET: Lectures
        public async Task<IActionResult> Index()
        {
            var coursesWithLectures = await _context.Courses
                .Include(c => c.Lectures)
                .OrderBy(c => c.CourseName)
                .ToListAsync();

            return View(coursesWithLectures);
        }


        // GET: Lectures/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var lecture = await _context.Lectures
                .Include(l => l.Course)
                .Include(l => l.Videos.OrderBy(v => v.DisplayOrder))
                .Include(l => l.Materials.OrderBy(m => m.CreatedAt))
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
        public IActionResult Create()
        {
            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName");
            return View();
        }

        // POST: Lectures/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("LectureId,Title,Description,VideoUrl,ThumbnailUrl,LectureDate,IsDeleted,CourseId")] Lecture lecture)
        {
            lecture.LectureId = Guid.NewGuid();
            _context.Add(lecture);
            await _context.SaveChangesAsync();

            await SendLectureNotificationsAsync(lecture);

            ViewData["CourseId"] = new SelectList(_context.Courses, "CourseId", "CourseName", lecture.CourseId);
            return RedirectToAction(nameof(Index));
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
    }
}
