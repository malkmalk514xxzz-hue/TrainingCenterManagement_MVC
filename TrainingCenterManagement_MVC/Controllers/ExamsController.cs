using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        public ExamsController(ApplicationDbContext context, IExamService examService)
        {
            _context = context;
            _examService = examService;
        }

        // ══════════════════════════════════════════════════════════
        //  TRAINER — إدارة الامتحانات
        // ══════════════════════════════════════════════════════════

        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Index()
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            var exams = await _context.Exams
                .Include(e => e.Course)
                .Include(e => e.ExamQuestions)
                .Include(e => e.ExamAttempts)
                .Where(e => e.TrainerId == trainerId)
                .OrderByDescending(e => e.StartDateTime)
                .ToListAsync();

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
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            var courses = await _context.CourseTrainers
                .Where(ct => ct.TrainerId == trainerId)
                .Select(ct => ct.Course)
                .ToListAsync();

            ViewData["CourseId"] = new SelectList(courses, "CourseId", "CourseName");
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Create(CreateExamDto dto)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                var courses = await _context.CourseTrainers
                    .Where(ct => ct.TrainerId == trainerId)
                    .Select(ct => ct.Course)
                    .ToListAsync();
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
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            if (!ModelState.IsValid) return View(dto);

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
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            await _examService.DeleteExamAsync(id, trainerId.Value);
            TempData["Success"] = "تم حذف الامتحان.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Trainer,Admin")]
        public async Task<IActionResult> Publish(Guid id)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null) return Forbid();

            try
            {
                await _examService.PublishExamAsync(id, trainerId.Value);
                TempData["Success"] = "تم نشر الامتحان — سيظهر للطلاب الآن.";
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
            var trainerId = await GetCurrentTrainerIdAsync();
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
