using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.DTOs;
using TrainingCenterManagement_MVC.Models.Enums;
using TrainingCenterManagement_MVC.Services;

namespace TrainingCenterManagement_MVC.Controllers
{
    /// Controller خاص بالطالب أثناء تأدية الامتحان
    [Authorize(Roles = "Trainee")]
    public class ExamAttemptController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IExamService _examService;

        public ExamAttemptController(ApplicationDbContext context, IExamService examService)
        {
            _context = context;
            _examService = examService;
        }

        // ── بدء الامتحان ───────────────────────────────────────────

        /// صفحة التأكيد قبل البدء
        [HttpGet]
        public async Task<IActionResult> Start(Guid examId)
        {
            var exam = await _context.Exams
                .Include(e => e.ExamQuestions)
                .FirstOrDefaultAsync(e => e.ExamId == examId && e.IsPublished);

            if (exam == null) return NotFound("الامتحان غير موجود.");

            if (!exam.HasStarted)
                return View("WaitingRoom", exam);

            if (exam.HasEnded)
            {
                TempData["Error"] = "انتهى وقت الامتحان.";
                return RedirectToAction("MyExams", "Exams");
            }

            // التحقق من المحاولات السابقة
            var traineeId = await GetCurrentTraineeIdAsync();
            if (traineeId == null) return Forbid();

            var completedAttempts = await _context.ExamAttempts
                .CountAsync(a => a.ExamId == examId && a.TraineeId == traineeId
                    && (a.Status == AttemptStatus.Submitted || a.Status == AttemptStatus.TimedOut));

            if (completedAttempts >= exam.MaxAttempts)
            {
                TempData["Error"] = "لقد استنفدت عدد المحاولات المسموح بها.";
                return RedirectToAction("MyExams", "Exams");
            }

            ViewBag.CompletedAttempts = completedAttempts;
            return View("ConfirmStart", exam);
        }

        /// POST: الضغط على "ابدأ الامتحان"
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(Guid examId, string confirm)
        {
            if (confirm != "yes") return RedirectToAction(nameof(Start), new { examId });

            var traineeId = await GetCurrentTraineeIdAsync();
            if (traineeId == null) return Forbid();

            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers["User-Agent"].ToString();

                var attempt = await _examService.StartExamAsync(
                    examId, traineeId.Value, ipAddress!, userAgent);

                return RedirectToAction(nameof(TakeExam), new { attemptId = attempt.AttemptId });
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("MyExams", "Exams");
            }
        }

        // ── صفحة الامتحان الرئيسية ────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> TakeExam(Guid attemptId)
        {
            var traineeId = await GetCurrentTraineeIdAsync();
            if (traineeId == null) return Forbid();

            var attempt = await _examService.ResumeExamAsync(attemptId, traineeId.Value);

            if (attempt == null)
            {
                TempData["Error"] = "انتهت المحاولة أو وقت الامتحان.";
                return RedirectToAction("MyExams", "Exams");
            }

            return View(attempt);
        }

        // ── Auto-Save (AJAX) ──────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> SaveAnswer([FromBody] SaveAnswerDto dto)
        {
            var traineeId = await GetCurrentTraineeIdAsync();
            if (traineeId == null) return Unauthorized();

            try
            {
                await _examService.SaveAnswerAsync(dto, traineeId.Value);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ── إرسال الامتحان نهائياً ────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(SubmitExamDto dto)
        {
            var traineeId = await GetCurrentTraineeIdAsync();
            if (traineeId == null) return Forbid();

            try
            {
                var result = await _examService.SubmitExamAsync(dto, traineeId.Value);
                TempData["Success"] = "تم إرسال الامتحان بنجاح.";
                return RedirectToAction(nameof(Result), new { attemptId = dto.AttemptId });
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(TakeExam), new { attemptId = dto.AttemptId });
            }
        }

        // ── صفحة النتيجة ──────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Result(Guid attemptId)
        {
            var traineeId = await GetCurrentTraineeIdAsync();
            if (traineeId == null) return Forbid();

            var result = await _examService.GetAttemptResultAsync(attemptId, traineeId.Value);

            if (result == null)
                return View("ResultPending");

            return View(result);
        }

        // ── Anti-Cheat: تسجيل تغيير التبويب (AJAX) ───────────────

        [HttpPost]
        public async Task<IActionResult> RecordTabSwitch([FromBody] TabSwitchDto dto)
        {
            var traineeId = await GetCurrentTraineeIdAsync();
            if (traineeId == null) return Unauthorized();

            await _examService.RecordTabSwitchAsync(dto.AttemptId, traineeId.Value);
            return Ok();
        }

        // ── HELPER ────────────────────────────────────────────────

        private async Task<Guid?> GetCurrentTraineeIdAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainee = await _context.Trainees
                .FirstOrDefaultAsync(t => t.UserId == userId);
            return trainee?.TraineeId;
        }
    }
}
