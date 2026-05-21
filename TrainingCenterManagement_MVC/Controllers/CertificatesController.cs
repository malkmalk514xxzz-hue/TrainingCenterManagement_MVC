using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class CertificatesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CertificatesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Certificates
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Certificates
                    .Include(c => c.Course)
                    .Include(c => c.Exam)
                    .Include(c => c.Trainee)
                        .ThenInclude(t => t.User) // لجلب بيانات المستخدم
                    .Include(c => c.Trainer); return View(await applicationDbContext.ToListAsync());
        }

        // GET: Certificates/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var certificate = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Exam)
                .Include(c => c.Trainee)
                    .ThenInclude(t => t.User) // لجلب بيانات المستخدم
                .Include(c => c.Trainer)
                .FirstOrDefaultAsync(m => m.CertificateId == id);

            if (certificate == null)
            {
                return NotFound();
            }

            return View(certificate);
        }


        // GET: Certificates/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            // Only trainees list — Course/Trainer/Exam are loaded on-demand via AJAX
            ViewData["TraineeId"] = new SelectList(
                _context.Trainees
                    .Include(t => t.User)
                    .Select(t => new { t.TraineeId, Display = t.User.FullName + " (" + t.User.Email + ")" }),
                "TraineeId", "Display");
            return View();
        }

        // POST: Certificates/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Average,IsDeleted,TraineeId,TrainerId,CourseId,ExamId")] Certificate certificate)
        {
            certificate.CertificateId = Guid.NewGuid();
            // Auto-generate verification URL — no manual entry needed
            certificate.Url = Url.Action("Verify", "Certificates",
                new { id = certificate.CertificateId }, Request.Scheme) ?? string.Empty;

            _context.Add(certificate);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ── AJAX helpers for cascading dropdowns ─────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetCoursesByTrainee(Guid traineeId)
        {
            var courses = await _context.CourseTrainees
                .Where(ct => ct.TraineeId == traineeId)
                .Include(ct => ct.Course)
                .Select(ct => new { value = ct.CourseId, text = ct.Course.CourseName })
                .ToListAsync();
            return Json(courses);
        }

        [HttpGet]
        public async Task<IActionResult> GetTrainersByCourse(Guid courseId)
        {
            var trainers = await _context.CourseTrainers
                .Where(ct => ct.CourseId == courseId)
                .Include(ct => ct.Trainer).ThenInclude(t => t.User)
                .Select(ct => new { value = ct.TrainerId, text = ct.Trainer.User.FullName + " (" + ct.Trainer.User.Email + ")" })
                .ToListAsync();
            return Json(trainers);
        }

        [HttpGet]
        public async Task<IActionResult> GetExamsByCourse(Guid courseId)
        {
            var exams = await _context.Exams
                .Where(e => e.CourseId == courseId)
                .Select(e => new { value = e.ExamId, text = e.ExamName })
                .ToListAsync();
            return Json(exams);
        }

        [HttpGet]
        public async Task<IActionResult> GetAttemptScore(Guid examId, Guid traineeId)
        {
            var attempt = await _context.ExamAttempts
                .Where(a => a.ExamId == examId && a.TraineeId == traineeId)
                .OrderByDescending(a => a.ScorePercentage)
                .FirstOrDefaultAsync();

            if (attempt?.ScorePercentage != null)
                return Json(new { score = Math.Round((double)attempt.ScorePercentage, 1) });

            return Json(new { score = (double?)null });
        }

        // GET: Certificates/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var certificate = await _context.Certificates
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Course)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .Include(c => c.Exam)
                .FirstOrDefaultAsync(c => c.CertificateId == id);

            if (certificate == null) return NotFound();

            // Trainee list (all)
            ViewData["TraineeId"] = new SelectList(
                _context.Trainees.Include(t => t.User)
                    .Select(t => new { t.TraineeId, Display = t.User.FullName + " (" + t.User.Email + ")" }),
                "TraineeId", "Display", certificate.TraineeId);

            // Current course/trainer/exam for this certificate (cascade will refresh on change)
            ViewData["CourseId"]   = new SelectList(
                _context.CourseTrainees.Where(ct => ct.TraineeId == certificate.TraineeId)
                    .Include(ct => ct.Course)
                    .Select(ct => new { ct.CourseId, ct.Course.CourseName }),
                "CourseId", "CourseName", certificate.CourseId);

            ViewData["TrainerId"]  = new SelectList(
                _context.CourseTrainers.Where(ct => ct.CourseId == certificate.CourseId)
                    .Include(ct => ct.Trainer).ThenInclude(t => t.User)
                    .Select(ct => new { ct.TrainerId, Display = ct.Trainer.User.FullName + " (" + ct.Trainer.User.Email + ")" }),
                "TrainerId", "Display", certificate.TrainerId);

            ViewData["ExamId"]     = new SelectList(
                _context.Exams.Where(e => e.CourseId == certificate.CourseId)
                    .Select(e => new { e.ExamId, e.ExamName }),
                "ExamId", "ExamName", certificate.ExamId);

            return View(certificate);
        }

        // POST: Certificates/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("CertificateId,Average,IsDeleted,TraineeId,TrainerId,CourseId,ExamId")] Certificate certificate)
        {
            if (id != certificate.CertificateId) return NotFound();

            // Preserve existing URL or generate one if missing
            var existingUrl = await _context.Certificates
                .AsNoTracking()
                .Where(c => c.CertificateId == id)
                .Select(c => c.Url)
                .FirstOrDefaultAsync();

            certificate.Url = string.IsNullOrEmpty(existingUrl)
                ? Url.Action("Verify", "Certificates", new { id = certificate.CertificateId }, Request.Scheme) ?? string.Empty
                : existingUrl;

            try
            {
                _context.Update(certificate);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CertificateExists(certificate.CertificateId)) return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Certificates/Delete/5
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var certificate = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Exam)
                .Include(c => c.Trainee)
                    .ThenInclude(t => t.User) // لجلب بيانات المستخدم
                .Include(c => c.Trainer)
                .FirstOrDefaultAsync(m => m.CertificateId == id);

            if (certificate == null)
            {
                return NotFound();
            }

            return View(certificate);
        }


        // POST: Certificates/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var certificate = await _context.Certificates.FindAsync(id);
            if (certificate != null)
            {
                _context.Certificates.Remove(certificate);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CertificateExists(Guid id)
        {
            return _context.Certificates.Any(e => e.CertificateId == id);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Verify(Guid id)
        {
            var cert = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CertificateId == id);

            if (cert == null) return NotFound();

            return View("VerifyCertificate", cert);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Search(string traineeName, string courseName)
        {
            var results = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .Where(c =>
                    (string.IsNullOrEmpty(traineeName) || c.Trainee.User.FullName.Contains(traineeName)) &&
                    (string.IsNullOrEmpty(courseName) || c.Course.CourseName.Contains(courseName))
                ).ToListAsync();

            return View("SearchResults", results);
        }

        [Authorize(Roles = "Admin, Trainer,Trainee")]
        public async Task<IActionResult> CertificatesByCourse(Guid courseId)
        {
            var certificates = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .Where(c => c.CourseId == courseId)
                .ToListAsync();

            ViewBag.CourseId = courseId;
            return View(certificates);
        }

        [HttpGet]
        public async Task<IActionResult> CertificatePdf(Guid? id, Guid? certificateId)
        {
            var targetId = id ?? certificateId ?? Guid.Empty;
            var certificate = await _context.Certificates
                .Include(c => c.Course)
                .Include(c => c.Trainee).ThenInclude(t => t.User)
                .Include(c => c.Trainer).ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CertificateId == targetId);

            if (certificate == null)
                return NotFound();

            var safeName = string.Concat(
                $"{certificate.Trainee.User.FullName}_{certificate.Course.CourseName}"
                .Split(System.IO.Path.GetInvalidFileNameChars()));

            return new ViewAsPdf("CertificatePdf", certificate)
            {
                FileName    = $"Certificate_{safeName}.pdf",
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape,
                PageSize    = Rotativa.AspNetCore.Options.Size.A4,
                PageMargins = new Rotativa.AspNetCore.Options.Margins(0, 0, 0, 0),
                CustomSwitches = "--disable-smart-shrinking --print-media-type"
            };

        }

    }
}
