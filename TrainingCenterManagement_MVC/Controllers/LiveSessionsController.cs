using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;
using Messaging_Chat_Application_MahmoudHakim.Hubs;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize(Roles = "Admin,Trainer")]
    public class LiveSessionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _hubContext;

        public LiveSessionsController(ApplicationDbContext context, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // GET: LiveSessions
        public async Task<IActionResult> Index()
        {
            var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            var sessions = await _context.LiveSessions
                .Include(ls => ls.Course)
                .Include(ls => ls.CreatedBy)
                .Where(ls => isAdmin || ls.CreatedByUserId == userId)
                .OrderByDescending(ls => ls.ScheduledAt)
                .ToListAsync();

            return View(sessions);
        }

        // GET: LiveSessions/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Courses = new SelectList(await GetAllowedCoursesAsync(), "CourseId", "CourseName");
            return View();
        }

        // POST: LiveSessions/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LiveSession model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            model.LiveSessionId   = Guid.NewGuid();
            model.JitsiRoomName   = $"tcm-{model.LiveSessionId:N}";
            model.CreatedByUserId = userId;
            model.CreatedAt       = DateTime.UtcNow;
            model.IsCancelled     = false;
            // Store as UTC — the datetime-local input arrives as local (Unspecified kind)
            model.ScheduledAt = DateTime.SpecifyKind(model.ScheduledAt, DateTimeKind.Local).ToUniversalTime();

            _context.LiveSessions.Add(model);
            await _context.SaveChangesAsync();

            await _context.Entry(model).Reference(ls => ls.Course).LoadAsync();
            await PushLiveSessionEventAsync(model, "created");

            TempData["Success"] = "تم جدولة الجلسة بنجاح وتم إشعار الطلاب المسجلين.";
            return RedirectToAction(nameof(Index));
        }

        // GET: LiveSessions/Edit/id
        public async Task<IActionResult> Edit(Guid id)
        {
            var session = await _context.LiveSessions
                .Include(ls => ls.Course)
                .FirstOrDefaultAsync(ls => ls.LiveSessionId == id);

            if (session == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && session.CreatedByUserId != userId) return Forbid();

            ViewBag.Courses = new SelectList(await GetAllowedCoursesAsync(), "CourseId", "CourseName", session.CourseId);
            // Convert UTC back to local for the datetime-local input
            ViewBag.ScheduledAtLocal = session.ScheduledAt.ToLocalTime().ToString("yyyy-MM-ddTHH:mm");
            return View(session);
        }

        // POST: LiveSessions/Edit/id
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, LiveSession model)
        {
            var session = await _context.LiveSessions.FindAsync(id);
            if (session == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && session.CreatedByUserId != userId) return Forbid();

            session.Title           = model.Title;
            session.Description     = model.Description;
            session.CourseId        = model.CourseId;
            session.DurationMinutes = model.DurationMinutes;
            session.ScheduledAt     = DateTime.SpecifyKind(model.ScheduledAt, DateTimeKind.Local).ToUniversalTime();

            await _context.SaveChangesAsync();

            await _context.Entry(session).Reference(ls => ls.Course).LoadAsync();
            await PushLiveSessionEventAsync(session, "edited");

            TempData["Success"] = "تم تعديل الجلسة وإشعار الطلاب بالتغيير.";
            return RedirectToAction(nameof(Index));
        }

        // GET: LiveSessions/Join/id  (all authenticated roles)
        [AllowAnonymous]
        [Authorize]
        public async Task<IActionResult> Join(Guid id)
        {
            var session = await _context.LiveSessions
                .Include(ls => ls.Course)
                .Include(ls => ls.CreatedBy)
                .FirstOrDefaultAsync(ls => ls.LiveSessionId == id);

            if (session == null || session.IsCancelled)
                return NotFound("الجلسة غير موجودة أو تم إلغاؤها.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && !User.IsInRole("Trainer"))
            {
                var trainee = await _context.Trainees.FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainee == null) return Forbid();
                var enrolled = await _context.CourseTrainees
                    .AnyAsync(ct => ct.CourseId == session.CourseId && ct.TraineeId == trainee.TraineeId);
                if (!enrolled) return Forbid();
            }

            var user = await _context.Users.FindAsync(userId);
            ViewBag.DisplayName = user?.FullName ?? user?.UserName ?? "مستخدم";
            ViewBag.IsModerator = User.IsInRole("Admin") || User.IsInRole("Trainer");
            ViewBag.IsHost      = session.CreatedByUserId == userId;

            return View(session);
        }

        // POST: LiveSessions/Cancel/id
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var session = await _context.LiveSessions
                .Include(ls => ls.Course)
                .FirstOrDefaultAsync(ls => ls.LiveSessionId == id);
            if (session == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && session.CreatedByUserId != userId) return Forbid();

            session.IsCancelled = true;
            await _context.SaveChangesAsync();

            await PushLiveSessionEventAsync(session, "cancelled");

            TempData["Success"] = "تم إلغاء الجلسة وتم إشعار الطلاب.";
            return RedirectToAction(nameof(Index));
        }

        // ── helpers ───────────────────────────────────────────────────────

        private async Task<List<Course>> GetAllowedCoursesAsync()
        {
            var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            IQueryable<Course> q = _context.Courses.Where(c => !c.IsDeleted);
            if (!isAdmin)
            {
                var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainer != null)
                    q = q.Where(c => c.CourseTrainers.Any(ct => ct.TrainerId == trainer.TrainerId));
            }
            return await q.ToListAsync();
        }

        /// <summary>
        /// Saves DB notifications + pushes real-time SignalR events to enrolled trainees.
        /// eventType: "created" | "edited" | "cancelled"
        /// </summary>
        private async Task PushLiveSessionEventAsync(LiveSession session, string eventType)
        {
            var course = session.Course ?? await _context.Courses.FindAsync(session.CourseId);
            if (course == null) return;

            var traineeUserIds = await _context.CourseTrainees
                .Where(ct => ct.CourseId == session.CourseId)
                .Include(ct => ct.Trainee)
                .Select(ct => ct.Trainee.UserId)
                .ToListAsync();

            if (!traineeUserIds.Any()) return;

            var localTime   = session.ScheduledAt.ToLocalTime().ToString("yyyy/MM/dd hh:mm tt");
            var (title, msg) = eventType switch
            {
                "created"   => ($"📅 جلسة مباشرة جديدة — {course.CourseName}", $"{session.Title} | {localTime}"),
                "edited"    => ($"✏️ تم تعديل جلسة — {course.CourseName}",    $"{session.Title} | {localTime}"),
                "cancelled" => ($"❌ تم إلغاء جلسة — {course.CourseName}",    $"{session.Title}"),
                _           => ($"جلسة — {course.CourseName}",                $"{session.Title}")
            };

            // DB notifications
            var notifications = traineeUserIds.Select(uid => new UserNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId         = uid,
                Title          = title,
                Message        = msg,
                Type           = NotificationType.LiveSessionScheduled,
                IsRead         = false,
                CreatedAt      = DateTime.UtcNow,
                RelatedId      = session.LiveSessionId.ToString()
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            // Real-time SignalR push
            foreach (var uid in traineeUserIds)
            {
                var connIds = await _context.UserConnections
                    .Where(c => c.UserId == uid && c.IsConnected)
                    .Select(c => c.ConnectionId)
                    .ToListAsync();

                foreach (var connId in connIds)
                {
                    // Toast notification (existing handler)
                    await _hubContext.Clients.Client(connId)
                        .SendAsync("ReceiveNotification", title, msg);

                    // Dashboard live-session widget update
                    await _hubContext.Clients.Client(connId)
                        .SendAsync("LiveSessionUpdate", new
                        {
                            eventType       = eventType,
                            sessionId       = session.LiveSessionId.ToString(),
                            title           = session.Title,
                            courseName      = course.CourseName,
                            scheduledAt     = session.ScheduledAt.ToString("o"),     // ISO UTC
                            scheduledLocal  = localTime,
                            isCancelled     = session.IsCancelled
                        });
                }
            }
        }
    }
}
