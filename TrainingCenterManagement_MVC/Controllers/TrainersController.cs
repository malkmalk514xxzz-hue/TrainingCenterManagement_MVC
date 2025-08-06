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
using TrainingCenterManagement_MVC.Models;
using TrainingCenterManagement_MVC.ViewModels;

namespace TrainingCenterManagement_MVC.Controllers
{
    public class TrainersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TrainersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Trainers
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Trainers.Include(t => t.User);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Trainers/Details/5
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trainer = await _context.Trainers
                .Include(t => t.User)
                .FirstOrDefaultAsync(m => m.TrainerId == id);
            if (trainer == null)
            {
                return NotFound();
            }

            return View(trainer);
        }

        // GET: Trainers/Create
        [Authorize(Roles = "Admin")]

        public IActionResult Create()
        {
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: Trainers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Create([Bind("TrainerId,UserId,Specialty,YearsOfExperience,BusinessLink")] Trainer trainer)
        {
            if (ModelState.IsValid)
            {
                trainer.TrainerId = Guid.NewGuid();
                _context.Add(trainer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", trainer.UserId);
            return View(trainer);
        }

        // GET: Trainers/Edit/5
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trainer = await _context.Trainers.FindAsync(id);
            if (trainer == null)
            {
                return NotFound();
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", trainer.UserId);
            return View(trainer);
        }

        // POST: Trainers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Edit(Guid id, [Bind("TrainerId,UserId,Specialty,YearsOfExperience,BusinessLink")] Trainer trainer)
        {
            if (id != trainer.TrainerId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(trainer);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TrainerExists(trainer.TrainerId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", trainer.UserId);
            return View(trainer);
        }

        // GET: Trainers/Delete/5
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trainer = await _context.Trainers
                .Include(t => t.User)
                .FirstOrDefaultAsync(m => m.TrainerId == id);
            if (trainer == null)
            {
                return NotFound();
            }

            return View(trainer);
        }

        // POST: Trainers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]

        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var trainer = await _context.Trainers.FindAsync(id);
            if (trainer != null)
            {
                _context.Trainers.Remove(trainer);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TrainerExists(Guid id)
        {
            return _context.Trainers.Any(e => e.TrainerId == id);
        }
        [Authorize(Roles = "Trainer")]
        public async Task<IActionResult> MyCoursesAttendance()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainer == null) return NotFound();

            var courses = await _context.CourseTrainers
                .Where(ct => ct.TrainerId == trainer.TrainerId)
                .Select(ct => ct.Course)
                .Include(c => c.Lectures)
                    .ThenInclude(l => l.Presences)
                .Include(c => c.CourseTrainees)
                    .ThenInclude(ct => ct.Trainee)
                    .ThenInclude(t => t.User)
                .ToListAsync();

            var model = new List<CourseAttendanceSummaryViewModel>();

            foreach (var course in courses)
            {
                var totalLectures = course.Lectures.Count;

                var attendanceSummaries = course.CourseTrainees.Select(ct =>
                {
                    var attended = course.Lectures.Count(l =>
                        l.Presences.Any(p => p.TraineeId == ct.TraineeId && p.IsPresent));

                    return new TraineeAttendanceViewModel
                    {
                        FullName = ct.Trainee.User.FullName,
                        TotalLectures = totalLectures,
                        AttendedLectures = attended,
                        AttendancePercentage = totalLectures > 0 ? (attended * 100.0 / totalLectures) : 0
                    };
                }).ToList();

                model.Add(new CourseAttendanceSummaryViewModel
                {
                    CourseName = course.CourseName,
                    TraineeAttendances = attendanceSummaries
                });
            }

            return View(model);
        }
        [Authorize(Roles = "Trainer")]
        public async Task<IActionResult> ExportAllCoursesToExcel()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainer == null) return NotFound();

            var courses = await _context.CourseTrainers
                .Where(ct => ct.TrainerId == trainer.TrainerId)
                .Select(ct => ct.Course)
                .Include(c => c.Lectures)
                    .ThenInclude(l => l.Presences)
                .Include(c => c.CourseTrainees)
                    .ThenInclude(ct => ct.Trainee)
                    .ThenInclude(t => t.User)
                .ToListAsync();

            using var package = new OfficeOpenXml.ExcelPackage();

            foreach (var course in courses)
            {
                var sheet = package.Workbook.Worksheets.Add(course.CourseName);
                sheet.Cells[1, 1].Value = "Student";
                sheet.Cells[1, 2].Value = "Attended";
                sheet.Cells[1, 3].Value = "Total";
                sheet.Cells[1, 4].Value = "Percentage";

                var totalLectures = course.Lectures.Count;
                var i = 2;

                foreach (var ct in course.CourseTrainees)
                {
                    var attended = course.Lectures.Count(l =>
                        l.Presences.Any(p => p.TraineeId == ct.TraineeId && p.IsPresent));
                    var percent = totalLectures > 0 ? Math.Round((attended * 100.0 / totalLectures), 2) : 0;

                    sheet.Cells[i, 1].Value = ct.Trainee.User.FullName;
                    sheet.Cells[i, 2].Value = attended;
                    sheet.Cells[i, 3].Value = totalLectures;
                    sheet.Cells[i, 4].Value = percent;
                    i++;
                }
            }

            var stream = new MemoryStream(package.GetAsByteArray());
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "AllCoursesAttendance.xlsx");
        }
        [Authorize(Roles = "Trainer")]
        public async Task<IActionResult> ExportAllCoursesToPdf()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);
            if (trainer == null) return NotFound();

            var courses = await _context.CourseTrainers
                .Where(ct => ct.TrainerId == trainer.TrainerId)
                .Select(ct => ct.Course)
                .Include(c => c.Lectures)
                    .ThenInclude(l => l.Presences)
                .Include(c => c.CourseTrainees)
                    .ThenInclude(ct => ct.Trainee)
                    .ThenInclude(t => t.User)
                .ToListAsync();

            var model = new List<CourseAttendanceSummaryViewModel>();

            foreach (var course in courses)
            {
                var totalLectures = course.Lectures.Count;
                var attendanceSummaries = course.CourseTrainees.Select(ct =>
                {
                    var attended = course.Lectures.Count(l =>
                        l.Presences.Any(p => p.TraineeId == ct.TraineeId && p.IsPresent));

                    return new TraineeAttendanceViewModel
                    {
                        FullName = ct.Trainee.User.FullName,
                        TotalLectures = totalLectures,
                        AttendedLectures = attended,
                        AttendancePercentage = totalLectures > 0 ? (attended * 100.0 / totalLectures) : 0
                    };
                }).ToList();

                model.Add(new CourseAttendanceSummaryViewModel
                {
                    CourseName = course.CourseName,
                    TraineeAttendances = attendanceSummaries
                });
            }

            return new Rotativa.AspNetCore.ViewAsPdf("MyCoursesAttendance", model)
            {
                FileName = "TrainerCoursesAttendance.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Landscape
            };
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        public async Task<IActionResult> MyCourses()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var trainer = await _context.Trainers.FirstOrDefaultAsync(t => t.UserId == userId);

            var courses = await _context.CourseTrainers
                .Where(ct => ct.TrainerId == trainer.TrainerId)
                .Select(ct => ct.Course)
                .ToListAsync();

            return View(courses);
        }

        // ✅ عرض المتدربين في كورس معين
        [Authorize(Roles = "Trainer")]

        public async Task<IActionResult> ViewTrainees(Guid courseId)
        {
            var course = await _context.Courses
                .Include(c => c.CourseTrainees)
                    .ThenInclude(ct => ct.Trainee)
                        .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null) return NotFound();

            ViewBag.CourseId = courseId;
            ViewBag.CourseName = course.CourseName;
            return View(course.CourseTrainees.Select(ct => ct.Trainee).ToList());
        }

        // ✅ إعادة توجيه لتسجيل الحضور (محاضرة)
        public IActionResult MarkAttendance(Guid lectureId)
        {
            return RedirectToAction("MarkAttendance", "Lectures", new { id = lectureId });
        }

        // ✅ إصدار شهادة لمتدرب في كورس
        public async Task<IActionResult> IssueCertificate(Guid traineeId, Guid courseId)
        {
            var course = await _context.Courses
                .Include(c => c.Exam)
                .Include(c => c.CourseTrainees)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null || course.Exam == null)
                return NotFound("Course or exam not found.");

            var trainee = await _context.Trainees.Include(t => t.User).FirstOrDefaultAsync(t => t.TraineeId == traineeId);
            if (trainee == null) return NotFound();

            var trainerId = await _context.Trainers
                .Where(t => t.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier))
                .Select(t => t.TrainerId)
                .FirstOrDefaultAsync();

            var certificate = new Certificate
            {
                CertificateId = Guid.NewGuid(),
                Average = 100, // أو احسب من نتيجة الاختبار
                Url = "", // يمكنك لاحقًا توليد PDF وتحميله
                CourseId = courseId,
                TrainerId = trainerId,
                TraineeId = traineeId,
                ExamId = course.Exam.ExamId
            };

            _context.Certificates.Add(certificate);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Certificate issued successfully.";
            return RedirectToAction(nameof(ViewTrainees), new { courseId = courseId });
        }
    }
}
