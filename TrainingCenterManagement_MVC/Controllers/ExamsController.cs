using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Messaging_Chat_Application_MahmoudHakim.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.DTOs;
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.Models.Enums;
using TrainingCenterManagement_MVC.Services;

namespace TrainingCenterManagement_MVC.Controllers
{
    [Authorize]
    public class ExamsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IExamService _examService;
        private readonly IHubContext<ChatHub> _hubContext;

        public ExamsController(ApplicationDbContext context, IExamService examService, IHubContext<ChatHub> hubContext)
        {
            _context = context;
            _examService = examService;
            _hubContext = hubContext;
        }

        // ══════════════════════════════════════════════════════════
        //  TRAINER — إدارة الامتحانات
        // ══════════════════════════════════════════════════════════

        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Index(
                      int page = 1,
                      int pageSize = 9,
                      string search = "",
                      string filter = "all")   // all | active | upcoming | draft | ended
        {
            pageSize = Math.Clamp(pageSize, 6, 30);

            IQueryable<Exam> query;

            if (User.IsInRole("Admin"))
            {
                query = _context.Exams
                    .Include(e => e.Course)
                    .Include(e => e.Trainer).ThenInclude(t => t.User)
                    .Include(e => e.ExamQuestions)
                    .Include(e => e.ExamAttempts)
                    .AsQueryable();
                ViewBag.IsAdmin = true;
            }
            else
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);
                if (trainer == null) return Unauthorized();

                query = _context.Exams
                    .Include(e => e.Course)
                    .Include(e => e.ExamQuestions)
                    .Include(e => e.ExamAttempts)
                    .Where(e => e.TrainerId == trainer.TrainerId)
                    .AsQueryable();
                ViewBag.IsAdmin = false;
            }

            // ── Search ─────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(e =>
                    e.ExamName.ToLower().Contains(term) ||
                    (e.Course.CourseName != null && e.Course.CourseName.ToLower().Contains(term)));
            }

            // ── Filter ─────────────────────────────────────────────────────
            query = filter switch
            {
                "active" => query.Where(e => e.IsActive),
                "upcoming" => query.Where(e => !e.HasStarted && e.IsPublished),
                "draft" => query.Where(e => !e.IsPublished),
                "ended" => query.Where(e => e.HasStarted && !e.IsActive && e.IsPublished),
                _ => query
            };

            // ── Stats from full unfiltered list ─────────────────────────────
            // FIX: await cannot be used inside a ternary — resolved separately
            List<Exam> allExams;
            if (User.IsInRole("Admin"))
            {
                allExams = await _context.Exams
                    .Include(e => e.ExamQuestions)
                    .Include(e => e.ExamAttempts)
                    .ToListAsync();
            }
            else
            {
                var statsUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var statsTrainer = await _context.Trainers
                    .FirstOrDefaultAsync(t => t.UserId == statsUserId);

                allExams = statsTrainer == null
                    ? new List<Exam>()
                    : await _context.Exams
                        .Include(e => e.ExamQuestions)
                        .Include(e => e.ExamAttempts)
                        .Where(e => e.TrainerId == statsTrainer.TrainerId)
                        .ToListAsync();
            }

            ViewBag.TotalAll = allExams.Count;
            ViewBag.TotalActive = allExams.Count(e => e.IsActive);
            ViewBag.TotalPending = allExams.Count(e => !e.HasStarted && e.IsPublished);
            ViewBag.TotalDrafts = allExams.Count(e => !e.IsPublished);
            ViewBag.TotalEnded = allExams.Count(e => e.HasStarted && !e.IsActive && e.IsPublished);

            // ── Paging ─────────────────────────────────────────────────────
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var exams = await query
                .OrderByDescending(e => e.StartDateTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;
            ViewBag.Search = search;
            ViewBag.Filter = filter;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_ExamsGridPartial", exams);

            return View(exams);
        }

        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Details(Guid id)
        {
            var exam = await _context.Exams
                .Include(e => e.Course)
                .Include(e => e.Trainer).ThenInclude(t => t.User)
                .Include(e => e.ExamQuestions).ThenInclude(eq => eq.Question)
                .Include(e => e.ExamAttempts)
                .FirstOrDefaultAsync(e => e.ExamId == id);

            if (exam == null) return NotFound();
            return View(exam);
        }

        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Create()
        {
            List<Course> courses;
            if (User.IsInRole("Admin"))
            {
                courses = await _context.Courses.Where(c => !c.IsDeleted).ToListAsync();
                ViewBag.IsAdmin = true;
            }
            else
            {
                var trainerId = await GetCurrentTrainerIdAsync();
                if (trainerId == null) return Forbid();
                courses = await _context.CourseTrainers
                    .Where(ct => ct.TrainerId == trainerId)
                    .Select(ct => ct.Course)
                    .ToListAsync();
            }
            ViewData["CourseId"] = new SelectList(courses, "CourseId", "CourseName");
            if (User.IsInRole("Admin"))
                ViewData["TrainerId"] = new SelectList(await _context.Trainers.Include(t => t.User).ToListAsync(), "TrainerId", "User.FullName");
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Create(CreateExamDto dto)
        {
            Guid? trainerId;
            if (User.IsInRole("Admin"))
            {
                Guid.TryParse(Request.Form["SelectedTrainerId"], out var selectedTid);
                trainerId = selectedTid != Guid.Empty ? selectedTid : (Guid?)null;
                if (trainerId == null) ModelState.AddModelError("", "يجب اختيار مدرب.");
            }
            else
            {
                trainerId = await GetCurrentTrainerIdAsync();
            }

            if (trainerId == null && !User.IsInRole("Admin")) return Forbid();

            if (!ModelState.IsValid || (User.IsInRole("Admin") && trainerId == null))
            {
                List<Course> courses;
                if (User.IsInRole("Admin"))
                {
                    courses = await _context.Courses.Where(c => !c.IsDeleted).ToListAsync();
                    ViewData["TrainerId"] = new SelectList(await _context.Trainers.Include(t => t.User).ToListAsync(), "TrainerId", "User.FullName");
                    ViewBag.IsAdmin = true;
                }
                else
                {
                    courses = await _context.CourseTrainers
                        .Where(ct => ct.TrainerId == trainerId)
                        .Select(ct => ct.Course)
                        .ToListAsync();
                }
                ViewData["CourseId"] = new SelectList(courses, "CourseId", "CourseName", dto.CourseId);
                return View(dto);
            }

            try
            {
                var exam = await _examService.CreateExamAsync(dto, trainerId.Value);
                TempData["Success"] = "تم إنشاء الامتحان بنجاح.";
                return RedirectToAction(nameof(ManageQuestions), new { id = exam.ExamId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(dto);
            }
        }

        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var exam = await _context.Exams.FindAsync(id);
            if (exam == null) return NotFound();

            var dto = new UpdateExamDto
            {
                ExamId = exam.ExamId,
                ExamName = exam.ExamName,
                Instructions = exam.Instructions,
                StartDateTime = exam.StartDateTime,
                DurationMinutes = exam.DurationMinutes,
                PassingScore = exam.PassingScore,
                MaxAttempts = exam.MaxAttempts,
                IsRandomized = exam.IsRandomized,
                ShowResultsImmediately = exam.ShowResultsImmediately
            };
            return View(dto);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Edit(UpdateExamDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            Guid? trainerId;
            if (User.IsInRole("Admin"))
            {
                var exam = await _context.Exams.FindAsync(dto.ExamId);
                trainerId = exam?.TrainerId;
            }
            else
            {
                trainerId = await GetCurrentTrainerIdAsync();
            }
            if (trainerId == null) return Forbid();

            try
            {
                await _examService.UpdateExamAsync(dto, trainerId.Value);
                TempData["Success"] = "تم تحديث الامتحان.";
                return RedirectToAction(nameof(Details), new { id = dto.ExamId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(dto);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            Guid? trainerId;
            if (User.IsInRole("Admin"))
            {
                var exam = await _context.Exams.FindAsync(id);
                trainerId = exam?.TrainerId;
            }
            else
            {
                trainerId = await GetCurrentTrainerIdAsync();
            }
            if (trainerId == null) return Forbid();

            await _examService.DeleteExamAsync(id, trainerId.Value);
            TempData["Success"] = "تم حذف الامتحان.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Publish(Guid id)
        {
            Guid? trainerId;
            if (User.IsInRole("Admin"))
            {
                var exam = await _context.Exams.FindAsync(id);
                trainerId = exam?.TrainerId;
            }
            else
            {
                trainerId = await GetCurrentTrainerIdAsync();
            }
            if (trainerId == null) return Forbid();

            try
            {
                await _examService.PublishExamAsync(id, trainerId.Value);
                TempData["Success"] = "تم نشر الامتحان — سيظهر للطلاب الآن.";
                await SendExamPublishedNotificationsAsync(id);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Unpublish(Guid id)
        {
            Guid? trainerId;
            if (User.IsInRole("Admin"))
            {
                var exam = await _context.Exams.FindAsync(id);
                trainerId = exam?.TrainerId;
            }
            else
            {
                trainerId = await GetCurrentTrainerIdAsync();
            }
            if (trainerId == null) return Forbid();

            try
            {
                await _examService.UnpublishExamAsync(id, trainerId.Value);
                TempData["Success"] = "تم إلغاء نشر الامتحان.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── إدارة الأسئلة ──────────────────────────────────────────

        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> ManageQuestions(Guid id)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            var exam = await _context.Exams
                .Include(e => e.ExamQuestions).ThenInclude(eq => eq.Question)
                .FirstOrDefaultAsync(e => e.ExamId == id && e.TrainerId == trainerId);

            if (exam == null) return NotFound();

            var questionBank = await _examService.GetQuestionBankAsync(trainerId.Value);

            ViewBag.Exam = exam;
            ViewBag.QuestionBank = questionBank;
            return View(exam.ExamQuestions.OrderBy(eq => eq.OrderIndex).ToList());
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> AddQuestion(AddQuestionToExamDto dto)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            try
            {
                await _examService.AddQuestionToExamAsync(dto, trainerId.Value);
                TempData["Success"] = "تمت إضافة السؤال.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(ManageQuestions), new { id = dto.ExamId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> RemoveQuestion(Guid examId, Guid questionId)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            try
            {
                await _examService.RemoveQuestionFromExamAsync(examId, questionId, trainerId.Value);
                TempData["Success"] = "تم حذف السؤال من الامتحان.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(ManageQuestions), new { id = examId });
        }

        // ── بنك الأسئلة ────────────────────────────────────────────

        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> QuestionBank(QuestionType? type = null, DifficultyLevel? difficulty = null)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            var questions = await _examService.GetQuestionBankAsync(trainerId.Value, type, difficulty);
            ViewBag.SelectedType = type;
            ViewBag.SelectedDifficulty = difficulty;
            return View(questions);
        }

        [Authorize(Roles = "Trainer,Admin")]
        public IActionResult CreateQuestion(Guid? examId = null)
        {
            ViewBag.ExamId = examId;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> CreateQuestion(CreateQuestionDto dto, Guid? examId = null)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                ViewBag.ExamId = examId;
                return View(dto);
            }

            try
            {
                var question = await _examService.CreateQuestionAsync(dto, trainerId.Value);

                // إذا تم الإنشاء من داخل صفحة إدارة الامتحان، أضفه مباشرة
                if (examId.HasValue)
                {
                    await _examService.AddQuestionToExamAsync(new AddQuestionToExamDto
                    {
                        ExamId = examId.Value,
                        QuestionId = question.QuestionId
                    }, trainerId.Value);
                    return RedirectToAction(nameof(ManageQuestions), new { id = examId.Value });
                }

                TempData["Success"] = "تمت إضافة السؤال لبنك الأسئلة.";
                return RedirectToAction(nameof(QuestionBank));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                ViewBag.ExamId = examId;
                return View(dto);
            }
        }

        // ── نتائج الطلاب ──────────────────────────────────────────

        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Results(Guid id)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            try
            {
                var results = await _examService.GetExamResultsForTrainerAsync(id, trainerId.Value);
                return View(results);
            }
            catch
            {
                return NotFound();
            }
        }

        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> AttemptDetail(Guid id)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            var result = await _examService.GetAttemptDetailForTrainerAsync(id, trainerId.Value);
            return View(result);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> ManualGrade(ManualGradeDto dto, Guid attemptId)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            try
            {
                await _examService.ManualGradeAnswerAsync(dto, trainerId.Value);
                TempData["Success"] = "تم حفظ التصحيح.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(AttemptDetail), new { id = attemptId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> ApplyPenalty(ApplyPenaltyDto dto)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "بيانات العقوبة غير مكتملة.";
                return RedirectToAction(nameof(AttemptDetail), new { id = dto.AttemptId });
            }

            if (dto.PenaltyType != PenaltyType.ZeroGrade && (dto.DeductionValue == null || dto.DeductionValue <= 0))
            {
                TempData["Error"] = dto.PenaltyType == PenaltyType.DeductPoints
                    ? "يجب إدخال عدد النقاط المراد خصمها."
                    : "يجب إدخال النسبة المئوية المراد خصمها.";
                return RedirectToAction(nameof(AttemptDetail), new { id = dto.AttemptId });
            }

            try
            {
                var ok = await _examService.ApplyPenaltyAsync(dto, trainerId.Value);
                TempData[ok ? "Success" : "Error"] = ok
                    ? "تم تطبيق العقوبة وتعديل الدرجة بنجاح."
                    : "لم يتم العثور على المحاولة أو لا يمكن تعديلها.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(AttemptDetail), new { id = dto.AttemptId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> RemovePenalty(Guid attemptId)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            var ok = await _examService.RemovePenaltyAsync(attemptId, trainerId.Value);
            TempData[ok ? "Success" : "Error"] = ok
                ? "تم إلغاء العقوبة وإعادة الدرجة الأصلية."
                : "لا توجد عقوبة مطبّقة على هذه المحاولة.";
            return RedirectToAction(nameof(AttemptDetail), new { id = attemptId });
        }

        // ══════════════════════════════════════════════════════════
        //  TRAINEE — رؤية الامتحانات المتاحة
        // ══════════════════════════════════════════════════════════

        [Authorize(Roles = "Trainee")]
        public async Task<IActionResult> MyExams()
        {
            var traineeId = await GetCurrentTraineeIdAsync();
            if (traineeId == null) return Forbid();

            var exams = await _examService.GetAvailableExamsForTraineeAsync(traineeId.Value);
            return View(exams);
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════

        private async Task SendExamPublishedNotificationsAsync(Guid examId)
        {
            var exam = await _context.Exams
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.ExamId == examId);

            if (exam?.Course == null) return;

            var startFormatted = exam.StartDateTime.ToString("dd/MM/yyyy HH:mm");
            var title   = "امتحان جديد";
            var message = $"تمت إضافة امتحان '{exam.ExamName}' في كورس {exam.Course.CourseName} — يبدأ في {startFormatted}";

            // جلب جميع المتدربين المسجّلين في الكورس
            var enrolledTrainees = await _context.CourseTrainees
                .Where(ct => ct.CourseId == exam.CourseId)
                .Include(ct => ct.Trainee)
                .ToListAsync();

            if (!enrolledTrainees.Any()) return;

            // حفظ الإشعارات في قاعدة البيانات
            var notifications = enrolledTrainees.Select(ct => new UserNotification
            {
                NotificationId = Guid.NewGuid(),
                UserId         = ct.Trainee.UserId,
                Title          = title,
                Message        = message,
                Type           = NotificationType.ExamAdded,
                IsRead         = false,
                CreatedAt      = DateTime.UtcNow,
                RelatedId      = examId.ToString()
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            // إرسال SignalR لكل متدرب متصل حالياً
            foreach (var ct in enrolledTrainees)
            {
                var connections = await _context.UserConnections
                    .Where(c => c.UserId == ct.Trainee.UserId && c.IsConnected)
                    .Select(c => c.ConnectionId)
                    .ToListAsync();

                foreach (var connId in connections)
                {
                    await _hubContext.Clients.Client(connId)
                        .SendAsync("ReceiveSystemNotification", title, message);
                }
            }
        }

        private async Task<Guid?> GetCurrentTrainerIdAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainer = await _context.Trainers
                .FirstOrDefaultAsync(t => t.UserId == userId);
            return trainer?.TrainerId;
        }

        private async Task<Guid?> GetCurrentTraineeIdAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees
                .FirstOrDefaultAsync(t => t.UserId == userId);
            return trainee?.TraineeId;
        }
    }
}
